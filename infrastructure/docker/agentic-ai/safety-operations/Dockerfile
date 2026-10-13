# syntax=docker/dockerfile:1

# Install the Safety & Operations package in the DHI development image and run
# it from the selected minimal DHI Python runtime image.
FROM dhi.io/python:3.13-alpine-dev AS build

WORKDIR /build

COPY services/agents/safety-operations/pyproject.toml ./pyproject.toml
COPY services/agents/safety-operations/src/ ./src/

RUN python3 -m pip install --disable-pip-version-check --no-cache-dir --no-compile --no-deps --target=/install .

FROM dhi.io/python:3.13-alpine AS runtime

WORKDIR /app

COPY --from=build /install/ /opt/agent-site/

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PYTHONPATH=/opt/agent-site \
    PORT=8080 \
    SAFETY_OPERATIONS_GEMINI_ENABLED=false

USER 65532:65532

EXPOSE 8080

CMD ["python3", "-m", "blueverse_safety_operations"]
