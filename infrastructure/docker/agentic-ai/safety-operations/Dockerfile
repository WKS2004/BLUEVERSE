# syntax=docker/dockerfile:1

# Proposal package only. Safety Operations dispatch remains unimplemented.
FROM dhi.io/python:3.13-alpine

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /app

COPY services/agents/__init__.py /app/services/agents/__init__.py
COPY services/agents/safety_operations /app/services/agents/safety_operations

ENV PYTHONPATH=/app

USER 65532:65532
CMD ["python3", "--version"]
