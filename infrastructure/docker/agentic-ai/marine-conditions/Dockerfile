# syntax=docker/dockerfile:1

FROM dhi.io/python:3.13-alpine

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PORT=8080

WORKDIR /app

COPY services/agents/marine-conditions/ /app/

EXPOSE 8080

CMD ["python3", "app.py"]
