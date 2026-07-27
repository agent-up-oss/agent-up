{
  description = "Agent-Up JetBrains plugin development environment";

  inputs.nixpkgs.url = "nixpkgs";

  outputs = { self, nixpkgs, ... }:
    let
      systems = [ "x86_64-linux" "aarch64-linux" ];
      forEachSystem = nixpkgs.lib.genAttrs systems;
    in
    {
      packages = forEachSystem (system:
        let
          pkgs = import nixpkgs { inherit system; };
          gradlewFhsScript = pkgs.writeShellScript "agent-up-jetbrains-gradlew-fhs-run" ''
            if [ "''${1:-}" = "--fhs-exec" ]; then
              shift
              exec "$@"
            fi

            export AGENTUP_JETBRAINS_GRADLEW_FHS=1
            exec ./gradlew --no-daemon "$@"
          '';
        in
        {
          gradlew-fhs = pkgs.buildFHSEnv {
            name = "agent-up-jetbrains-gradlew-fhs";
            targetPkgs = fhsPkgs: with fhsPkgs; [
              bash
              fontconfig
              freetype
              glib
              gtk3
              libGL
              libice
              libsm
              libx11
              zlib
            ];
            runScript = "${gradlewFhsScript}";
          };
        });

      apps = forEachSystem (system: {
        gradlew-fhs = {
          type = "app";
          program = "${self.packages.${system}.gradlew-fhs}/bin/agent-up-jetbrains-gradlew-fhs";
        };
      });
    };
}
