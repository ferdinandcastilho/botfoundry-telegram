#!/usr/bin/env bash
# Sends the result of a finished workflow run to the Telegram DevOps bot.
# Inputs come only from environment variables set by notify.yml; nothing from
# the pull request is ever executed or interpolated into this script.
#
# Required: WORKFLOW CONCLUSION REPO BRANCH ACTOR SHA RUN_URL RUN_NUMBER
# Optional: PR (pull request number), BOT_TOKEN, CHAT_ID, GH_TOKEN (to read the latest release),
#           DRY_RUN=1 (print the message instead of sending it)
#
# Every message has the same layout:
#   <emoji> <title>
#   <repository>
#
#   Workflow / Branch / Commit / By / PR / Version   (fields that apply)
#
#   <links>
set -euo pipefail

if [ -z "${BOT_TOKEN:-}" ] || [ -z "${CHAT_ID:-}" ]; then
  echo "Telegram secrets not available (fork or not configured); skipping."
  exit 0
fi

esc() { printf '%s' "$1" | sed -e 's/&/\&amp;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g'; }

# Long branch names (Dependabot) are shortened in the middle.
short() {
  local s="$1"
  if [ "${#s}" -gt 40 ]; then printf '%s…%s' "${s:0:18}" "${s: -18}"; else printf '%s' "$s"; fi
}

NL=$'\n'
emoji=""; title=""; version=""; release_url=""

case "$CONCLUSION" in
  success)
    case "$WORKFLOW" in
      "Publish alpha")
        emoji="🧪"; title="Alpha published"
        ;;
      "Publish stable")
        emoji="🚀"; title="Stable release published"
        if [ -n "${GH_TOKEN:-}" ]; then
          version="$(gh api "repos/${REPO}/releases/latest" --jq .tag_name 2>/dev/null || true)"
          release_url="$(gh api "repos/${REPO}/releases/latest" --jq .html_url 2>/dev/null || true)"
        fi
        ;;
      *) exit 0 ;;
    esac
    ;;
  failure|startup_failure)
    emoji="❌"; title="$WORKFLOW failed"
    ;;
  cancelled|timed_out)
    case "$WORKFLOW" in "Publish alpha"|"Publish stable") ;; *) exit 0 ;; esac
    emoji="⚠️"; title="$WORKFLOW ${CONCLUSION/_/ }"
    ;;
  *) exit 0 ;;
esac

msg="${emoji} <b>$(esc "$title")</b>${NL}<i>$(esc "$REPO")</i>${NL}"
msg+="${NL}<b>Workflow:</b> $(esc "$WORKFLOW")"
msg+="${NL}<b>Branch:</b> <code>$(esc "$(short "$BRANCH")")</code>"
msg+="${NL}<b>Commit:</b> <code>${SHA:0:7}</code>"
msg+="${NL}<b>By:</b> $(esc "$ACTOR")"
[ -n "${PR:-}" ] && msg+="${NL}<b>PR:</b> #$(esc "$PR")"
[ -n "$version" ] && msg+="${NL}<b>Version:</b> <code>$(esc "$version")</code>"

links="🔗 <a href=\"${RUN_URL}\">Run #${RUN_NUMBER}</a>"
[ -n "${PR:-}" ] && links+=" · <a href=\"https://github.com/${REPO}/pull/$(esc "$PR")\">PR #$(esc "$PR")</a>"
[ -n "$release_url" ] && links+=" · <a href=\"${release_url}\">Release</a>"
msg+="${NL}${NL}${links}"

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
