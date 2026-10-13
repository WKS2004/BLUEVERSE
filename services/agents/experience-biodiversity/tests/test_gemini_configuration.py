from blueverse_experience_agent.gemini_client import DEFAULT_GEMINI_MODEL, GeminiConfig


def test_experience_agent_reads_only_its_own_gemini_key(monkeypatch):
    monkeypatch.delenv("EXPERIENCE_BIODIVERSITY_GEMINI_API_KEY", raising=False)
    monkeypatch.delenv("EXPERIENCE_BIODIVERSITY_GEMINI_ENABLED", raising=False)
    monkeypatch.setenv("GEMINI_API_KEY", "must-not-be-shared")

    config = GeminiConfig.from_env()

    assert config.api_key is None
    assert config.model == DEFAULT_GEMINI_MODEL


def test_experience_agent_reads_its_scoped_gemini_configuration(monkeypatch):
    monkeypatch.setenv("EXPERIENCE_BIODIVERSITY_GEMINI_API_KEY", "experience-test-key")
    monkeypatch.setenv("EXPERIENCE_BIODIVERSITY_GEMINI_MODEL", "gemini-test-model")
    monkeypatch.setenv("EXPERIENCE_BIODIVERSITY_GEMINI_ENABLED", "true")

    config = GeminiConfig.from_env()

    assert config.api_key == "experience-test-key"
    assert config.model == "gemini-test-model"
