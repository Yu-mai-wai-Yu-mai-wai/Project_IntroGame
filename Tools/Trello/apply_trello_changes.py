#!/usr/bin/env python3
"""Apply the changes in trello_changes.json to the Trello board.

Usage:
    export TRELLO_KEY=...      # https://trello.com/power-ups/admin -> API key
    export TRELLO_TOKEN=...    # generated from the same page
    python apply_trello_changes.py              # dry run: prints the plan, changes nothing
    python apply_trello_changes.py --apply      # writes to the board

Everything is matched by name, so the JSON stays readable and editable. The script
never deletes or archives anything, and skips work that is already done (re-running is safe).
Standard library only.
"""
import argparse
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request

API = "https://api.trello.com/1"


class Trello:
    def __init__(self, key, token):
        self.auth = {"key": key, "token": token}

    def call(self, method, path, params=None):
        query = dict(self.auth)
        body = None
        if method == "GET":
            query.update(params or {})
        elif params:
            body = urllib.parse.urlencode(params).encode("utf-8")
        url = f"{API}{path}?{urllib.parse.urlencode(query)}"
        req = urllib.request.Request(url, data=body, method=method)
        if body is not None:
            req.add_header("Content-Type", "application/x-www-form-urlencoded")
        try:
            with urllib.request.urlopen(req, timeout=30) as resp:
                return json.loads(resp.read().decode("utf-8") or "null")
        except urllib.error.HTTPError as e:
            raise RuntimeError(f"{method} {path} -> HTTP {e.code}: {e.read().decode('utf-8', 'replace')}") from None


def norm(text):
    return " ".join(text.split()).casefold()


class Board:
    """Name -> object lookups for one board, loaded once."""

    def __init__(self, api, board_id):
        self.api = api
        self.board = api.call("GET", f"/boards/{board_id}", {"fields": "name,id"})
        bid = self.board["id"]
        self.lists = api.call("GET", f"/boards/{bid}/lists", {"filter": "open", "fields": "name"})
        self.cards = api.call("GET", f"/boards/{bid}/cards",
                              {"filter": "open", "fields": "name,idList,idMembers,due,dueComplete",
                               "checklists": "all"})
        self.members = api.call("GET", f"/boards/{bid}/members", {"fields": "username,fullName"})
        self.labels = api.call("GET", f"/boards/{bid}/labels", {"fields": "name,color", "limit": "1000"})

    def _one(self, items, name, kind, key="name"):
        hits = [i for i in items if norm(i[key]) == norm(name)]
        if len(hits) != 1:
            raise LookupError(f"{kind} '{name}': {len(hits)} exact matches")
        return hits[0]

    def list_(self, name):
        return self._one(self.lists, name, "list")

    def card(self, name):
        return self._one(self.cards, name, "card")

    def has_card(self, name):
        return any(norm(c["name"]) == norm(name) for c in self.cards)

    def member(self, username):
        return self._one(self.members, username, "member", key="username")

    def label(self, name):
        return self._one(self.labels, name, "label")

    @staticmethod
    def check_item(card, fragment):
        hits = [(cl, it) for cl in card.get("checklists", []) for it in cl["checkItems"]
                if norm(fragment) in norm(it["name"])]
        if len(hits) != 1:
            raise LookupError(f"check item '{fragment}' on '{card['name']}': {len(hits)} matches")
        return hits[0][1]


def build_plan(board, spec):
    """Resolve every change to (description, method, path, params). Lookup errors are collected, not raised."""
    plan, errors = [], []

    def attempt(fn):
        try:
            fn()
        except LookupError as e:
            errors.append(str(e))

    for m in spec.get("move_cards", []):
        def f(m=m):
            card, dest = board.card(m["card"]), board.list_(m["to_list"])
            if card["idList"] != dest["id"]:
                plan.append((f"move '{card['name']}' -> {dest['name']}", "PUT", f"/cards/{card['id']}",
                             {"idList": dest["id"], "pos": "bottom"}))
        attempt(f)

    for name in spec.get("complete_cards", []):
        def f(name=name):
            card = board.card(name)
            if not card["dueComplete"]:
                plan.append((f"mark complete '{card['name']}'", "PUT", f"/cards/{card['id']}",
                             {"dueComplete": "true"}))
        attempt(f)

    for c in spec.get("check_items", []):
        def f(c=c):
            card = board.card(c["card"])
            item = board.check_item(card, c["item"])
            if item["state"] != "complete":
                plan.append((f"tick '{item['name'][:50]}' on '{card['name']}'", "PUT",
                             f"/cards/{card['id']}/checkItem/{item['id']}", {"state": "complete"}))
        attempt(f)

    for u in spec.get("update_cards", []):
        def f(u=u):
            card = board.card(u["card"])
            if u.get("due"):
                plan.append((f"due {u['due']} on '{card['name']}'", "PUT", f"/cards/{card['id']}",
                             {"due": u["due"]}))
            for username in u.get("add_members", []):
                member = board.member(username)
                if member["id"] not in card["idMembers"]:
                    plan.append((f"add @{username} to '{card['name']}'", "POST",
                                 f"/cards/{card['id']}/idMembers", {"value": member["id"]}))
        attempt(f)

    for a in spec.get("add_checklists", []):
        def f(a=a):
            card = board.card(a["card"])
            if any(norm(cl["name"]) == norm(a["name"]) for cl in card.get("checklists", [])):
                return
            plan.append((f"checklist '{a['name']}' ({len(a['items'])} items) on '{card['name']}'",
                         "CHECKLIST", card["id"], {"name": a["name"], "items": a["items"]}))
        attempt(f)

    for n in spec.get("new_cards", []):
        def f(n=n):
            if board.has_card(n["name"]):
                return
            params = {"idList": board.list_(n["list"])["id"], "name": n["name"], "pos": "bottom"}
            if n.get("desc"):
                params["desc"] = n["desc"]
            if n.get("due"):
                params["due"] = n["due"]
            if n.get("members"):
                params["idMembers"] = ",".join(board.member(u)["id"] for u in n["members"])
            if n.get("labels"):
                params["idLabels"] = ",".join(board.label(l)["id"] for l in n["labels"])
            plan.append((f"new card '{n['name']}' in {n['list']}", "NEWCARD", None,
                         {"card": params, "checklists": n.get("checklists", [])}))
        attempt(f)

    return plan, errors


def add_checklist(api, card_id, name, items):
    cl = api.call("POST", "/checklists", {"idCard": card_id, "name": name})
    for item in items:
        api.call("POST", f"/checklists/{cl['id']}/checkItems", {"name": item, "pos": "bottom"})


def execute(api, plan):
    for desc, method, path, params in plan:
        print(f"  {desc} ...", end=" ", flush=True)
        if method == "CHECKLIST":
            add_checklist(api, path, params["name"], params["items"])
        elif method == "NEWCARD":
            card = api.call("POST", "/cards", params["card"])
            for cl in params["checklists"]:
                add_checklist(api, card["id"], cl["name"], cl["items"])
        else:
            api.call(method, path, params)
        print("ok")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--apply", action="store_true", help="write to the board (default is a dry run)")
    parser.add_argument("--file", default=os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                                       "trello_changes.json"))
    args = parser.parse_args()

    key, token = os.environ.get("TRELLO_KEY"), os.environ.get("TRELLO_TOKEN")
    if not key or not token:
        sys.exit("Set TRELLO_KEY and TRELLO_TOKEN first (see the docstring at the top of this file).")

    with open(args.file, encoding="utf-8") as fh:
        spec = json.load(fh)

    api = Trello(key, token)
    board = Board(api, spec["board"])
    print(f"Board: {board.board['name']}")

    plan, errors = build_plan(board, spec)
    if errors:
        print("\nCould not resolve (fix names in the JSON):")
        for e in errors:
            print(f"  - {e}")

    print(f"\n{len(plan)} change(s):")
    for desc, *_ in plan:
        print(f"  - {desc}")

    if not args.apply:
        print("\nDry run only. Re-run with --apply to write these changes.")
        return
    if errors:
        sys.exit("\nNot applying: fix the unresolved names above first.")
    print("\nApplying:")
    execute(api, plan)
    print("Done.")


if __name__ == "__main__":
    main()
