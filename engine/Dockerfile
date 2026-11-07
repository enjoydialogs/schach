# Engine-Dienst (SCHACH-SPEC 5.2), Image `schach-engine`, AGPL-3.0-or-later. Eingefroren ab Phase 0.
#
# S4a (#29): PyTorch CPU, Maia-3 (geaenderter CSSLab-Code in src/schach_engine/maia3/), Stockfish 19
# und Fairy-Stockfish 14 als gepinnte Release-Binaries mit sha256-Pruefung (docker/hole_binaries.py).
#
# Weitergabe (GPL-3.0 Abschnitt 6, docs/adr/0002-ghcr-pakete-oeffentlich.md): das Image traegt den
# GPL-Text und die Hinweise unter /usr/share/doc/schach-engine/ (COPYING, NOTICES.md).
#
# Gewichte nie im Repo und nie im Image (SCHACH-SPEC 13.6): Maia-3 laedt beim ersten Start von
# Hugging Face nach /models, dem Volume `engine-models` (src/schach_engine/modell_download.py).
#
# Python-Basis aus ECR Public (Docker Official Image, ohne Docker-Hub-Pull-Limit auf CI-Runnern).
# Debian slim, nicht Alpine: PyTorch-CPU-Wheels gibt es nur fuer glibc.
ARG PYTHON_REGISTRY=public.ecr.aws/docker/library

# --- Stufe 1: Binaries laden und pruefen -------------------------------------------------------
FROM ${PYTHON_REGISTRY}/python:3.12-slim-bookworm AS binaries

# Ziel-CPU (SCHACH-SPEC 5.2). Der VPS kann x86-64-avx512, avx2 bleibt Standard und Fallback.
# Stockfish 19 gibt es nur als Universal-Binary (waehlt AVX2/AVX-512 zur Laufzeit); SF_ARCH waehlt
# das Fairy-Stockfish-Build. Unbekannte Werte brechen den Build ab.
ARG SF_ARCH=x86-64-avx2

COPY docker/hole_binaries.py /tmp/hole_binaries.py
RUN python /tmp/hole_binaries.py --arch "${SF_ARCH}" --ziel /opt/engines --doc /opt/doc

# --- Stufe 2: Laufzeit ------------------------------------------------------------------------
FROM ${PYTHON_REGISTRY}/python:3.12-slim-bookworm

ARG SF_ARCH=x86-64-avx2
# PyTorch CPU-only vom offiziellen CPU-Index; ohne ihn zoege pip die CUDA-Wheels (mehrere GB).
ARG TORCH_VERSION=2.15.0

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    SF_ARCH=${SF_ARCH} \
    ENGINE_MODELS_DIR=/models \
    STOCKFISH_PATH=/usr/local/bin/stockfish \
    FAIRY_STOCKFISH_PATH=/usr/local/bin/fairy-stockfish

# Fairy-Stockfish ist dynamisch gegen libstdc++ gelinkt.
RUN apt-get update \
 && apt-get install -y --no-install-recommends libstdc++6 \
 && rm -rf /var/lib/apt/lists/*

COPY --from=binaries /opt/engines/ /usr/local/bin/
COPY --from=binaries /opt/doc/ /usr/share/doc/schach-engine/

# Rauchtest beim Bauen: beide Engines starten und melden `readyok`.
RUN printf 'uci\nisready\nquit\n' | stockfish | grep -q '^readyok' \
 && printf 'uci\nisready\nquit\n' | fairy-stockfish | grep -q '^readyok'

# Eigener Benutzer. /models MUSS im Image existieren und ihm gehoeren: ein Named Volume uebernimmt
# die Rechte nur von einem Pfad, den das Image anlegt; sonst scheitert der Modell-Download.
RUN useradd --uid 10001 --no-create-home --shell /usr/sbin/nologin engine \
 && mkdir -p /models \
 && chown engine /models

WORKDIR /app
RUN pip install --index-url https://download.pytorch.org/whl/cpu "torch==${TORCH_VERSION}"
COPY LICENSE pyproject.toml ./
COPY src/ src/
RUN pip install ".[model]" && rm -rf src

# Erst hier: der Commit wechselt bei jedem Build und darf die Schichten davor nicht neu bauen.
# release.yml setzt COMMIT_SHA; ohne ihn (lokaler oder CI-Testbau) steht `unbekannt` im Hinweis.
ARG COMMIT_SHA=unbekannt
RUN case "${COMMIT_SHA}" in *[!0-9a-z]*|"") echo "COMMIT_SHA muss ein Commit-SHA sein" >&2; exit 1 ;; esac \
 && sed -i -e "s/@COMMIT_SHA@/${COMMIT_SHA}/g" -e "s/@TORCH_VERSION@/${TORCH_VERSION}/g" \
      /usr/share/doc/schach-engine/NOTICES.md \
 && ! grep -q '@[A-Z_]*@' /usr/share/doc/schach-engine/NOTICES.md \
 && grep -q 'GNU GENERAL PUBLIC LICENSE' /usr/share/doc/schach-engine/COPYING

USER engine
EXPOSE 8081

# Ein Worker: Maia laeuft in einem Prozess (MAIA_TORCH_THREADS). Kein Access-Log: es enthielte die
# Client-Adresse, und IPs gehoeren nie in Logs (SCHACH-SPEC 14.3).
CMD ["uvicorn", "schach_engine.app:app", "--host", "0.0.0.0", "--port", "8081", "--workers", "1", "--no-access-log"]
