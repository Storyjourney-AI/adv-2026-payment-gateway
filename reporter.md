# Reporter Config

## MCP Server
- **Name**: reporter (tools appear as `mcp__reporter__*`)
- **Endpoint**: https://reporter.bynava.org/mcp
- **Auth**: `X-Api-Key` header. Key stored in `.mcp.json` under
  `mcpServers.reporter.headers["X-Api-Key"]` — read it from there, do not hardcode here.

## Default Target
- **Workspace ID**: 17b09cd3-6f7d-4767-9f19-665675a534d7
- **Project ID**: 89b58571-5e7b-48bf-8100-04790ed6973c
- **Project name**: Payment Gateway

Get IDs for another project from its app URL:
`https://reporter.bynava.org/dashboard/workspaces/{workspaceId}/projects/{projectId}`

## Available MCP tools
create_report, rename_report, delete_report, get_content, add_text, edit_text,
add_image, delete_element, rearrange, submit_report_probe

## Image upload (REST — not an MCP tool)
`add_image` mints a one-time upload token; the file is uploaded over REST:
```
curl -s -w "\nHTTP_STATUS:%{http_code}\n" -X POST \
  "https://reporter.bynava.org/api/reports/{reportId}/elements/image" \
  -H "X-Api-Key: <key from .mcp.json>" \
  -H "X-Upload-Token: <token from add_image>" \
  -F "file=@/absolute/path/to/image.jpg" \
  -F "caption=ADV-XXX ss-NN - short label" \
  -F "description=Figure N: one line"
```
Gotchas: use the **https** host (returned uploadUrl may be http); image **captions must be
ASCII** (use `-`, not `—` — multipart mangles non-ASCII); retry once on a transient **502**;
confirm each upload is **HTTP 200**.

## Report style
Manager-level, screenshot-first. One report per task. Title → header + short summary →
for each screenshot: [image] then [dot-point "What this shows"] → final result block.
Plain English, screenshots ordered to follow the user story / test flow.
(Full style guide lives in the `reporter-publish` skill.)
