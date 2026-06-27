# Symbolic Worker Prototype

This directory contains a local SymPy worker prototype for isolated symbolic operations.

For full project architecture and development rules, use the root [`README.md`](../../README.md).

## Purpose

- provide a stdin/stdout JSON worker prototype
- keep symbolic processing isolated from the main server flow
- support plain string input only

## Run

```bash
echo '{"operation":"limit","expression":"sin(x)/x","variable":"x","point":"0"}' | python3 Tools/Symbolic/symbolic_worker.py
```

## Supported Operations

- simplify
- expand
- factor
- diff
- integrate
- limit
- solve
- series

## Caveats

- input format is `plain/sympy` only
- LaTeX input is not supported
- output is JSON on stdout only
- the worker does not use `eval`
