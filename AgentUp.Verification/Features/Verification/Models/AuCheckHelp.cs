namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>Usage text for <c>au-check</c>. Exit codes are part of the contract a program consumes.</summary>
public static class AuCheckHelp
{
    public const string Text =
        """
        Usage: au-check [--repo <path>] [--config <path>] <command>

          plan                     Show the checks the current changes require
          run [<check-id>]         Run the required checks (or one of them) and record receipts
              [--report <dir>]     Write report.json, report.html, and report.png
          guard [--run]            Report whether every required check is proven
          coverage [--min N]       Measure coverage of the changed lines
          slices [--min N]         Report total coverage per feature slice

        --repo defaults to the working directory. --config defaults to agent-up.json
        in that directory. Receipts are written to the checkout's Git directory.

        Exit codes:
          0  every required check ran and passed
          1  a required check ran and failed
          2  a required check could not run because a toolchain is missing
          3  configuration is invalid, or a changed path matches no rule
        """;
}
