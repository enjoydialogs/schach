# Engine-Dienst (SCHACH-SPEC 5.2), Image `schach-engine`, AGPL-3.0-or-later. Eingefroren ab Phase 0.
#
# Phase 0c: der Stub mit Fixture-Antworten (src/schach_engine/fixture.py). S4a ergaenzt PyTorch CPU,
# Maia-3 und die gepinnten Stockfish-/Fairy-Stockfish-Binaries in einem kleinen Geruest-PR.
#
# Gewichte nie im Repo und nie im Image (SCHACH-SPEC 13.6): Maia-3 laedt beim ersten Start von
# Hugging Face nach /models, dem Volume `engine-models`.
#
# Python-Basis aus ECR Public (Docker Official Image, ohne Docker-Hub-Pull-Limit auf CI-Runnern).
# Debian slim, nicht Alpine: PyTorch-CPU-Wheels gibt es nur fuer glibc.
ARG PYTHON_REGISTRY=public.ecr.aws/docker/library
FROM ${PYTHON_REGISTRY}/python:3.12-slim-bookworm

# Stockfish-Build fuer die Ziel-CPU (SCHACH-SPEC 5.2). Der VPS kann x86-64-avx512, avx2 bleibt
# Fallback. Wird erst ab S4a gelesen; steht hier, damit S4a das Build-Argument nicht neu einfuehrt.
ARG SF_ARCH=x86-64-avx2

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    SF_ARCH=${SF_ARCH} \
    ENGINE_MODELS_DIR=/models

# Eigener Benutzer. /models MUSS im Image existieren und ihm gehoeren: ein Named Volume uebernimmt
# die Rechte nur von einem Pfad, den das Image anlegt; sonst scheitert der Modell-Download.
RUN useradd --uid 10001 --no-create-home --shell /usr/sbin/nologin engine \
 && mkdir -p /models \
 && chown engine /models

WORKDIR /app
COPY LICENSE pyproject.toml ./
COPY src/ src/
RUN pip install . && rm -rf src

USER engine
EXPOSE 8081

# Ein Worker: Maia laeuft in einem Prozess (MAIA_TORCH_THREADS). Kein Access-Log: es enthielte die
# Client-Adresse, und IPs gehoeren nie in Logs (SCHACH-SPEC 14.3).
CMD ["uvicorn", "schach_engine.app:app", "--host", "0.0.0.0", "--port", "8081", "--workers", "1", "--no-access-log"]
