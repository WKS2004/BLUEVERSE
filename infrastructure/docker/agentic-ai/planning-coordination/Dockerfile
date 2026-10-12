# syntax=docker/dockerfile:1

# Runtime-only image shell. The executable agent remains gated on G07 and its
# implementation belongs on agentic-ai/planning-coordination.
FROM dhi.io/python:3.13-alpine

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /app

CMD ["python3", "--version"]
