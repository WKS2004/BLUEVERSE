# syntax=docker/dockerfile:1

# Staged proposal package. Integrated dispatch remains gated on G07 and the
# assigned workflow implementation belongs on agentic-ai/safety-operations.
FROM dhi.io/python:3.13-alpine

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /app

COPY services/agents/__init__.py /app/services/agents/__init__.py
COPY services/agents/safety_operations /app/services/agents/safety_operations

ENV PYTHONPATH=/app

USER 65532:65532

CMD ["python3", "--version"]
