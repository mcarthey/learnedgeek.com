#!/usr/bin/env python3
"""Sync remote posts.json to local before editing."""

import requests
import json
import sys

REMOTE_URL = 'https://learnedgeek.com/Content/posts.json'
LOCAL_PATH = 'LearnedGeek/Content/posts.json'

print("Syncing posts.json from remote...")

try:
    # Download current remote posts.json
    response = requests.get(REMOTE_URL)
    response.raise_for_status()
    remote_posts = response.json()

    # Overwrite local with remote
    with open(LOCAL_PATH, 'w', encoding='utf-8') as f:
        json.dump(remote_posts, f, indent=2, ensure_ascii=False)

    print(f"Synced {len(remote_posts['posts'])} posts from remote")
    print("You can now safely edit posts.json locally")

except requests.exceptions.RequestException as e:
    print(f"Error downloading from remote: {e}")
    sys.exit(1)
except json.JSONDecodeError as e:
    print(f"Error parsing remote JSON: {e}")
    sys.exit(1)
except Exception as e:
    print(f"Unexpected error: {e}")
    sys.exit(1)
