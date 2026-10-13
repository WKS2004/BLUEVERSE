# syntax=docker/dockerfile:1

# The proxy uses only Python's standard library and the repository-selected DHI runtime.
FROM dhi.io/python:3.13-alpine

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /app

COPY services/agents/gemini_egress_proxy.py /app/gemini_egress_proxy.py

USER 65532:65532
EXPOSE 3128

CMD ["python3", "/app/gemini_egress_proxy.py"]
