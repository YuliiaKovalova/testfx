---
name: "Build Failure Analysis"
description: >-
  Runs `./build.sh --binaryLog` on every PR; when the build fails, delegates
  to the `build-failure-analyst` agent (which queries a containerized binlog
  MCP server) to identify root causes, post a PR comment summarizing them,
  and attach inline `suggestion` blocks tied to the diff.

# This workflow is **advisory**, not gating:
#  - It posts an analysis comment / inline suggestions when the build fails.
#  - It does NOT mark the PR check as failing on its own (gh-aw has no
#    post-agent step hook). The repository's deterministic build gate lives
#    in azure-pipelines.yml; if you want a GitHub Actions-level required
#    check, add a separate non-agentic `build.yml` workflow alongside this
#    one and configure branch protection accordingly.

on:
  pull_request:
    types: [opened, synchronize, reopened]
    branches: [main, 'rel/*']
    # Fork PRs are skipped: they cannot install from dotnet-tools (auth-gated)
    # and the agent token would lack the `pull-requests: write` scope needed
    # by safe-outputs.
    forks: []
  workflow_dispatch:
    inputs:
      pr-number:
        description: "PR number to scope inline suggestion comments to (optional)"
        required: false
        type: string
  # Manual reruns and dispatch invocations are restricted to repository
  # contributors. (`pull_request` already gets fork-blocking by default
  # via `forks: []`.) For a slash-command rerun path on PR comments, see
  # the companion `build-failure-analysis-command.md` workflow.
  roles: [admin, maintainer, write]
  reaction: "eyes"

permissions:
  contents: read
  pull-requests: read

concurrency:
  group: build-failure-analysis-${{ github.event.pull_request.number || github.event.issue.number || github.ref }}
  cancel-in-progress: true

env:
  NUGET_MCP_VERSION: '1.4.3'

timeout-minutes: 30

network:
  allowed:
    - defaults
    - dotnet

imports:
  - shared/build-failure-analysis-shared.md

# Deterministic setup that runs before the AI agent starts. By the time the
# agent boots: dotnet is on PATH, the binlog has been produced (whether the
# build succeeded or failed), and the binlog path and build outcome are
# exported as `GH_AW_*` env vars. The agent reads the binlog through the
# containerized `binlog` MCP server (see `mcp-servers` below), which is given a
# read-only mount of the workspace so it can open `GH_AW_BINLOG_PATH` directly.
#
# `continue-on-error: true` is essential on the build step: a failed build
# must not abort the job before the agent gets to analyse it.
steps:
  - name: Build with binary log
    id: build
    continue-on-error: true
    run: |
      set -o pipefail
      ./build.sh --binaryLog 2>&1 | tee /tmp/build-output.log

  - name: Put dotnet on the path
    if: always()
    run: echo "$PWD/.dotnet" >> $GITHUB_PATH

  - name: Locate binlog
    id: find-binlog
    run: |
      BINLOG=$(find artifacts/log -name '*.binlog' -type f -printf '%T@ %p\n' 2>/dev/null \
        | sort -rn | head -1 | cut -d' ' -f2-)
      if [ -n "$BINLOG" ] && [ -f "$BINLOG" ]; then
        BINLOG=$(realpath "$BINLOG")
        echo "found=true"   >> "$GITHUB_OUTPUT"
        echo "path=$BINLOG" >> "$GITHUB_OUTPUT"
      else
        echo "found=false" >> "$GITHUB_OUTPUT"
      fi

  - name: Install NuGet MCP Server
    if: steps.build.outcome == 'failure' && steps.find-binlog.outputs.found == 'true'
    continue-on-error: true
    run: dotnet tool install --global NuGet.Mcp.Server --version "$NUGET_MCP_VERSION"

  # On `workflow_dispatch` runs, `github.sha` is the SHA of the dispatched ref
  # (usually the default branch), NOT the PR head. Look up the real PR head
  # SHA via the API so permalinks and inline comment placement match the PR.
  - name: Resolve PR head SHA (workflow_dispatch only)
    if: github.event_name == 'workflow_dispatch' && inputs.pr-number != ''
    id: resolve-pr-sha
    env:
      GH_TOKEN: ${{ github.token }}
      GH_AW_GITHUB_REPOSITORY: ${{ github.repository }}
      GH_AW_INPUTS_PR_NUMBER: ${{ inputs.pr-number }}
    run: |
      SHA=$(gh api "repos/${GH_AW_GITHUB_REPOSITORY}/pulls/${GH_AW_INPUTS_PR_NUMBER}" --jq .head.sha)
      echo "sha=$SHA" >> "$GITHUB_OUTPUT"

  - name: Export agent context
    env:
      GH_AW_STEPS_BUILD_OUTCOME: ${{ steps.build.outcome }}
      GH_AW_BINLOG_PATH_VALUE: ${{ steps.find-binlog.outputs.path }}
      GH_AW_PR_NUMBER_VALUE: ${{ github.event.pull_request.number || github.event.issue.number || inputs.pr-number }}
      GH_AW_PR_HEAD_SHA_VALUE: ${{ steps.resolve-pr-sha.outputs.sha || github.event.pull_request.head.sha || github.sha }}
      GH_AW_GITHUB_WORKSPACE: ${{ github.workspace }}
    run: |
      {
        echo "GH_AW_BUILD_OUTCOME=${GH_AW_STEPS_BUILD_OUTCOME}"
        echo "GH_AW_BINLOG_PATH=${GH_AW_BINLOG_PATH_VALUE}"
        echo "GH_AW_PR_NUMBER=${GH_AW_PR_NUMBER_VALUE}"
        echo "GH_AW_PR_HEAD_SHA=${GH_AW_PR_HEAD_SHA_VALUE}"
        echo "GH_AW_WORKSPACE=${GH_AW_GITHUB_WORKSPACE}"
      } >> "$GITHUB_ENV"

tools:
  github:
    toolsets: [pull_requests, repos]
  bash:
    - "cat"
    - "head"
    - "tail"
    - "grep"
    - "wc"
    - "sort"
    - "uniq"
    - "ls"
    - "find"
    - "dotnet"
    - "NuGet.Mcp.Server"

mcp-servers:
  # Containerized Microsoft.AITools.BinlogMcp (dotnet/dotnet-buildtools-prereqs-docker#1660).
  # Exposes ~29 binlog inspection tools (binlog_overview, binlog_errors,
  # binlog_warnings, binlog_diagnose, binlog_search, binlog_task_details, …) to the
  # analyst agent, replacing the former DumpBinlog JSON shim. The gh-aw MCP gateway
  # launches it as a stdio container; the workspace is mounted read-only so the
  # server can open the binlog at `GH_AW_BINLOG_PATH` (an absolute path under the
  # workspace). The image bundles a pinned tool version, so no `BINLOG_MCP_VERSION`
  # is needed here.
  binlog:
    container: "mcr.microsoft.com/dotnet-buildtools/prereqs:azurelinux-3.0-binlog-mcp-amd64"
    mounts:
      - "${{ github.workspace }}:${{ github.workspace }}:ro"
    allowed: ["*"]

safe-outputs:
  add-comment:
    max: 1
    hide-older-comments: true
  create-pull-request-review-comment:
    max: 10
  noop:
    report-as-issue: false
---

<!--
  Body provided by shared/build-failure-analysis-shared.md.

  All build-failure analysis expertise (binlog parsing, error grouping,
  suggestion authoring) lives in the reusable agent at
  .github/agents/build-failure-analyst.agent.md.
-->
