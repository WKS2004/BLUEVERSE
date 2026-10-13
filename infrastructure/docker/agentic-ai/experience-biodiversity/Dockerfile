# syntax=docker/dockerfile:1

# Coastal Experience & Biodiversity Agent (Member 1 — Ushan Srinuka)
# Install the application in the DHI development image, then copy the installed
# package into the minimal DHI runtime image.
FROM dhi.io/python:3.13-alpine-dev AS dev

WORKDIR /build

COPY services/agents/experience-biodiversity/pyproject.toml ./pyproject.toml
COPY services/agents/experience-biodiversity/README.md ./README.md
COPY services/agents/experience-biodiversity/src/ ./src/

RUN python3 -m pip install --disable-pip-version-check --no-cache-dir --no-compile --target=/install .

FROM dhi.io/python:3.13-alpine AS runtime

WORKDIR /app

COPY --from=dev /install/ /opt/agent-site/

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PYTHONPATH="/opt/agent-site" \
    PORT=8080 \
    GEMINI_MODEL="gemini-3.6-flash" \
    GEMINI_API_BASE_URL="https://generativelanguage.googleapis.com/v1beta"

EXPOSE 8080

CMD ["python3", "-m", "blueverse_experience_agent.server"]
