# Code Generator History Detail UX Requirements

## Background

The generation history detail drawer works functionally, but the information hierarchy is weak. Summary fields, install steps, generated files, SQL draft, and raw request JSON are all presented with similar visual weight.

## Requirements

- Make the drawer explain the generation result at a glance.
- Put module, table, status, operator, file count, and conflict count in a clear overview.
- Present install guidance as a readable vertical flow instead of a cramped grid.
- Keep SQL draft and generated files easy to scan and copy/read.
- Keep raw request JSON available, but visually secondary.

## UX Critique Notes

- Automated scan: clean.
- Main issue: information architecture, not visual noise.
- Cognitive load: moderate because every detail has similar weight.
- Recommended direction: `/layout`, `/clarify`, then `/polish`.
