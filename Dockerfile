FROM mcr.microsoft.com/dotnet/core/sdk:3.1-alpine AS build-environment
LABEL maintainer="jskii <blackdanieljames@gmail.com>"
ADD . /src
WORKDIR /src
RUN dotnet restore 
RUN dotnet build -c Debug && \
    dotnet publish -c Debug -o build

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-alpine
LABEL maintainer="jskii <blackdanieljames@gmail.com>"
WORKDIR /app
COPY --from=0 /src/build .
ENTRYPOINT ["dotnet", "tsts.dll"]