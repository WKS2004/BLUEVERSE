# syntax=docker/dockerfile:1

# Coastal Experience & Biodiversity Agent (Member 1 — Ushan Srinuka)
# Executable Agentic AI role runtime using Hardened Alpine Python 3.13 image.
FROM dhi.io/python:3.13-alpine

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PYTHONPATH="/app/src" \
    PORT=8080

WORKDIR /app

# Copy application source code
COPY services/agents/experience-biodiversity/src/ /app/src/

EXPOSE 8080

CMD ["python3", "-m", "blueverse_experience_agent.server"]
