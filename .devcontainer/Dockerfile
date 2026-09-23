# Dev container image: tooling only. The deployable app image is built separately.
FROM mcr.microsoft.com/devcontainers/dotnet:10.0

ARG DEBIAN_FRONTEND=noninteractive

# psql for poking at the dev database
RUN apt-get update \
    && apt-get install -y --no-install-recommends postgresql-client \
    && rm -rf /var/lib/apt/lists/*

USER vscode

# Pre-create the Claude config dir so the named volume mounted over it
# is initialized with vscode ownership instead of root.
RUN mkdir -p /home/vscode/.claude

# Claude Code CLI (native installer, lands in ~/.local/bin)
RUN curl -fsSL https://claude.ai/install.sh | bash
ENV PATH="/home/vscode/.local/bin:${PATH}"

USER root
