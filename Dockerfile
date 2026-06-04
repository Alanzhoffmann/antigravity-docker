FROM mcr.microsoft.com/dotnet/nightly/sdk:11.0-preview as builder

# Install system dependencies
RUN apt-get update && apt-get install -y \
    curl git jq \
    && rm -rf /var/lib/apt/lists/*

# Install GitHub CLI (gh) for programmatic PR and Issue handling
RUN mkdir -p -m 755 /etc/apt/keyrings \
    && curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg | tee /etc/apt/keyrings/githubcli-archive-keyring.gpg > /dev/null \
    && chmod go+r /etc/apt/keyrings/githubcli-archive-keyring.gpg \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" | tee /etc/apt/sources.list.d/github-cli.list > /dev/null \
    && apt-get update && apt-get install gh -y

# Install the Google Antigravity CLI
RUN curl -fsSL https://antigravity.google/cli/install.sh | bash
ENV PATH="/root/.local/bin:${PATH}"

# Force git to use the GitHub CLI as its credential helper for HTTPS
RUN git config --global credential.https://github.com.helper '!gh auth git-credential'

# Set the global bot identity for automated commits
RUN git config --global user.name "Antigravity Bot" \
    && git config --global user.email "bot@jellybeanoutofhome.com"

# Setup the workspace and API
WORKDIR /app
COPY ./*.slnx ./nuget.config ./

COPY src/*/*.csproj ./
RUN for file in $(ls *.csproj); do mkdir -p src/${file%.*}/ && mv $file src/${file%.*}/; done

# COPY test/*/*.csproj ./
# RUN for file in $(ls *.csproj); do mkdir -p test/${file%.*}/ && mv $file test/${file%.*}/; done

RUN dotnet restore

COPY ./test ./test
COPY ./src ./src

RUN dotnet build -c Release

RUN dotnet publish "./src/PersonalBot.Api/PersonalBot.Api.csproj" -c Release -o "./dist" --no-restore

ENTRYPOINT ["dotnet", "./dist/PersonalBot.Api.dll"]