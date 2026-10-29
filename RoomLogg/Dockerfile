FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.3-resolute-aot AS build
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
RUN apt-get update \
    && apt-get install -y --no-install-recommends libudev1 libcap2 \
    && mkdir -p /native-libs \
    && cp -a --parents \
        /usr/lib/*-linux-gnu/libudev.so.1* \
        /usr/lib/*-linux-gnu/libcap.so.2* \
        /native-libs/ \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY global.json .
COPY ["RoomLogg/RoomLogg.csproj", "RoomLogg/"]
COPY ["RoomLogg.Device/RoomLogg.Device.csproj", "RoomLogg.Device/"]
RUN dotnet restore "RoomLogg/RoomLogg.csproj" -a "$TARGETARCH" -p:PublishAot=true
COPY . .
RUN dotnet publish "RoomLogg/RoomLogg.csproj" --no-restore \
    -c $BUILD_CONFIGURATION -a "$TARGETARCH" -o /app/publish \
    --self-contained true -p:PublishAot=true -p:TrimmerSingleWarn=false

FROM mcr.microsoft.com/dotnet/runtime-deps:11.0.0-rc.3-resolute-chiseled AS final
COPY --from=build /native-libs/ /
WORKDIR /app
USER $APP_UID
COPY --from=build /app/publish/RoomLogg .
ENTRYPOINT ["./RoomLogg"]
