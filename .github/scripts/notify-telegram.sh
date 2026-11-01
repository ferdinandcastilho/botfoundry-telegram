#!/usr/bin/env bash
# Sends the result of a finished workflow run to the Telegram DevOps bot.
# Inputs come only from environment variables set by notify.yml; nothing from
# the pull request is ever executed or interpolated into this script.
#
# Required: WORKFLOW CONCLUSION REPO BRANCH ACTOR SHA RUN_URL RUN_NUMBER
# Optional: PR (pull request number), BOT_TOKEN, CHAT_ID, GH_TOKEN (to read the latest release),
#           DRY_RUN=1 (print the message instead of sending it)
set -euo pipefail

if [ -z "${BOT_TOKEN:-}" ] || [ -z "${CHAT_ID:-}" ]; then
  echo "Telegram secrets not available (fork or not configured); skipping."
  exit 0
fi

esc() { printf '%s' "$1" | sed -e 's/&/\&amp;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g'; }

is_publish=false
case "$WORKFLOW" in "Publish alpha"|"Publish stable") is_publish=true ;; esac

repo="$(esc "$REPO")"
workflow="$(esc "$WORKFLOW")"
branch="$(esc "$BRANCH")"
actor="$(esc "$ACTOR")"
sha7="${SHA:0:7}"
link="<a href=\"${RUN_URL}\">View run #${RUN_NUMBER}</a>"

case "$CONCLUSION" in
  success)
    if [ "$WORKFLOW" = "Publish alpha" ]; then
      msg="🧪 <b>${repo}</b> — alpha published
Branch: <code>${branch}</code> · commit <code>${sha7}</code>
By: ${actor}
${link}"
    elif [ "$WORKFLOW" = "Publish stable" ]; then
      tag=""; release_url=""
      if [ -n "${GH_TOKEN:-}" ]; then
        tag="$(gh api "repos/${REPO}/releases/latest" --jq .tag_name 2>/dev/null || true)"
        release_url="$(gh api "repos/${REPO}/releases/latest" --jq .html_url 2>/dev/null || true)"
      fi
      title="stable release"
      [ -n "$tag" ] && title="stable release <b>$(esc "$tag")</b>"
      msg="🚀 <b>${repo}</b> — ${title}
Commit: <code>${sha7}</code> · by ${actor}"
      if [ -n "$release_url" ]; then
        msg="${msg}
<a href=\"${release_url}\">GitHub release</a> · ${link}"
      else
        msg="${msg}
${link}"
      fi
    else
      exit 0
    fi
    ;;
  failure|startup_failure)
    msg="❌ <b>${repo}</b> — ${workflow} failed
Branch: <code>${branch}</code>
By: ${actor}"
    if [ -n "${PR:-}" ]; then msg="${msg}
PR: #$(esc "$PR")"; fi
    msg="${msg}
${link}"
    ;;
  cancelled|timed_out)
    if [ "$is_publish" != true ]; then exit 0; fi
    state="cancelled"; [ "$CONCLUSION" = "timed_out" ] && state="timed out"
    msg="⚠️ <b>${repo}</b> — ${workflow} ${state}
Branch: <code>${branch}</code>
${link}"
    ;;
  *)
    exit 0
    ;;
esac

if [ "${DRY_RUN:-}" = "1" ]; then
  printf '%s\n' "$msg"
  exit 0
fi

# The token goes through a curl config on stdin so it never appears in the process list.
printf 'url = "https://api.telegram.org/bot%s/sendMessage"\n' "$BOT_TOKEN" | curl -fsS -K - \
  --data-urlencode "chat_id=${CHAT_ID}" \
  --data-urlencode "parse_mode=HTML" \
  --data-urlencode "disable_web_page_preview=true" \
  --data-urlencode "text=${msg}" > /dev/null
echo "Notification sent."
