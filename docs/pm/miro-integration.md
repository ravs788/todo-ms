# Miro Integration (Level 1: GitHub → Miro)

Status: Deferred  
Scope: One-way sync from GitHub Issues to Miro Delivery board (create/update cards on issue events)

Note: Integration is currently paused. The GitHub workflow is set to manual (workflow_dispatch) and will not react to issue events until re-enabled. See the "Enable later" guidance at the end of this document.

This guide documents how we integrate our engineering workflow with Miro so that issues automatically appear and update on a shared Delivery board. This is a one‑way automation (GitHub → Miro). Two‑way sync (Miro ↔ GitHub) can be added later.

Contents
- Overview
- Requirements
- Board Structure (Swimlanes)
- GitHub Labels (Taxonomy)
- Secrets and Configuration
- GitHub Actions Workflow
- How It Works (Event Mapping)
- Testing the Integration
- Troubleshooting
- Future Enhancements (Level 2)

---

Overview

- When a GitHub issue is opened (or labeled/edited), a Miro Card is created on the Delivery board in the lane for the owning service.
- The card shows:
  - Title: “[svc] Issue title (#123)”
  - Description: short summary + link to the issue
- On issue close, the card is moved to the “Done” lane (or flagged as Done).
- The workflow stores a link back to the created Miro Card in the GitHub issue comments and tracks the Miro Card ID for future updates.

---

Requirements

- A Miro board accessible to the engineering team:
  - Board URL: https://miro.com/app/board/uXjVGG4jN28=/
  - Board ID: uXjVGG4jN28=
- A Miro Personal Access Token (PAT) with minimal scopes:
  - boards:read, boards:write
- GitHub repository with Actions enabled
- Ability to add repo secrets (see “Secrets and Configuration” below)

---

Board Structure (Swimlanes)

We use a single Delivery board with service swimlanes (as Frames) and an optional Done lane. You can either:

A) Create frames in Miro named exactly:
- lane:auth
- lane:user
- lane:todo
- lane:tag
- lane:notify
- lane:admin
- lane:gateway
- lane:infra
- lane:done (optional)

OR

B) Skip frames and rely on coordinates:
We’ll place items at predefined coordinates per lane (see below). This works out of the box if frames aren’t available.

Default lane coordinates (center-origin; in Miro canvas units):
- auth:    x=-1800, y=-900
- user:    x=-1200, y=-900
- todo:    x= -600, y=-900
- tag:     x=    0, y=-900
- notify:  x=  600, y=-900
- admin:   x= 1200, y=-900
- gateway: x= 1800, y=-900
- infra:   x= 2400, y=-900
- done:    x=    0, y= 600

You may adjust these in the workflow env if needed.

---

GitHub Labels (Taxonomy)

Service ownership (required):
- svc:auth, svc:user, svc:todo, svc:tag, svc:notify, svc:admin, svc:gateway, svc:infra

Type (optional):
- type:feature, type:bug, type:techdebt

Priority (optional):
- prio:p0, prio:p1, prio:p2

The workflow determines the swimlane from the first svc:* label present on the issue. If none is found, it defaults to infra.

---

Secrets and Configuration

Add the following repository secrets (Settings → Secrets and variables → Actions → New repository secret):

- MIRO_TOKEN
  - Value: Your Miro Personal Access Token with boards:read, boards:write
- MIRO_DELIVERY_BOARD_ID
  - Value: The board ID (e.g., uXjVGG4jN28=)

Optional environment customizations in the workflow:
- LANE_COORDS_*: adjust coordinates if you prefer a different layout

---

GitHub Actions Workflow

We add .github/workflows/miro-sync.yml that triggers on issue events. It:
1) Resolves the service lane from labels.
2) Creates or updates the corresponding Miro Card on the Delivery board.
3) Stores the Miro Card ID in a GitHub issue comment (tagged “MIRO_CARD_ID:”).
4) On close, moves the card to the “done” lane (using the done coordinates).

Note: We use Miro Cards (v2 API). If your plan doesn’t have cards enabled, you can switch the “cards” endpoint to “sticky_notes” in the workflow.

---

How It Works (Event Mapping)

GitHub events handled:
- issues: opened → create Miro Card
- issues: labeled → create/update Miro Card (if service label added later)
- issues: edited → update card title/description
- issues: closed → move card to Done lane

Miro API used:
- POST https://api.miro.com/v2/boards/{board_id}/cards
- PATCH https://api.miro.com/v2/boards/{board_id}/cards/{card_id}

Payload shape (typical):
{
  "data": {
    "title": "[svc] Issue title (#123)",
    "description": "Issue body or summary…\n\nGitHub: https://github.com/org/repo/issues/123"
  },
  "position": {
    "origin": "center",
    "x": -600,
    "y": -900
  }
}

If frames are used and you capture a frame ID, you can add:
"parent": { "id": "<frameId>" }

---

Testing the Integration

1) Set MIRO_TOKEN and MIRO_DELIVERY_BOARD_ID secrets in GitHub.
2) Merge the miro-sync.yml workflow into default branch.
3) Open a new issue with a service label (e.g., svc:todo).
4) Within a few seconds, check the Miro Delivery board:
   - A card should appear in the Todo lane coordinates.
5) Close the issue:
   - The card should relocate to the Done lane coordinates.

If you change an issue title/body or add/remove service labels, a subsequent event will update the card.

---

Troubleshooting

- 401/403 from Miro API:
  - Ensure MIRO_TOKEN is valid and has boards:write scope.
  - Ensure the token user has access to the target board/team.

- 404 board not found:
  - Check MIRO_DELIVERY_BOARD_ID matches the board ID from the URL.

- Card not moving on close:
  - Confirm the workflow found a “MIRO_CARD_ID:” comment on the issue (created during the initial card creation).
  - Manually add a comment “MIRO_CARD_ID: <id>” if needed, then re-run workflow on an edit.

- No service label:
  - The workflow defaults to infra, or add any svc:* label.

---

Future Enhancements (Level 2)

- Miro webhooks → small serverless bridge to create/update GitHub issues when Cards are created/edited directly in Miro (two‑way sync).
- Frame ID targeting for precise placement within lanes.
- Enhanced visuals: tags/colors based on type/prio, and status badges.
