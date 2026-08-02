{ config, lib, pkgs, ... }:

let
  cfg = config.services.agent-up-server;
in
{
  options.services.agent-up-server = {
    enable = lib.mkEnableOption "Agent-Up server";

    package = lib.mkOption {
      type = lib.types.package;
      description = "The Agent-Up server package.";
    };

    dataDir = lib.mkOption {
      type = lib.types.str;
      default = "/var/lib/agent-up";
      description = "Directory where Agent-Up stores its persistent data.";
    };

    listenUrl = lib.mkOption {
      type = lib.types.str;
      default = "http://127.0.0.1:5000";
      description = "URL the HTTP server listens on.";
    };

    chromiumPackage = lib.mkOption {
      type = lib.types.package;
      default = pkgs.chromium;
      defaultText = lib.literalExpression "pkgs.chromium";
      description = "Chromium package used for headless browser sessions. Must be a NixOS-compatible build; PuppeteerSharp's downloaded binary is a dynamically-linked generic Linux binary that does not run on NixOS.";
    };
  };

  config = lib.mkIf cfg.enable {
    systemd.services.agent-up-server = {
      description = "Agent-Up Server";
      after = [ "network.target" ];
      wantedBy = [ "multi-user.target" ];

      serviceConfig = {
        Type = "simple";
        ExecStart = "${cfg.package}/AgentUp.Server --urls ${cfg.listenUrl}";
        Environment = [
          "ASPNETCORE_URLS=${cfg.listenUrl}"
          "Storage__DataDirectory=${cfg.dataDir}"
          "Browser__ExecutablePath=${cfg.chromiumPackage}/bin/chromium"
          "DOTNET_BUNDLE_EXTRACT_BASE_DIR=/var/cache/agent-up"
        ];
        StateDirectory = "agent-up";
        CacheDirectory = "agent-up";
        Restart = "on-failure";
        RestartSec = 5;
      };
    };
  };
}
