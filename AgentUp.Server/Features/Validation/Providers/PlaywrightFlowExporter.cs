using System.Text;
using System.Text.Json;
using AgentUp.Server.Features.Validation.DTOs;
namespace AgentUp.Server.Features.Validation.Providers;
public sealed class PlaywrightFlowExporter
{
    public PlaywrightExport Export(ValidationFlow flow)
    {
        var builder = new StringBuilder("import { test, expect } from '@playwright/test';\n\n");
        builder.Append("test(").Append(Q(flow.Name)).Append(", async ({ page }) => {\n");
        builder.Append("  await page.goto(new URL(").Append(Q(flow.InitialPath)).Append(", process.env.AGENT_UP_BASE_URL!).toString());\n");
        AppendAssertions(builder, flow.InitialExpectations, "  ");
        foreach (var step in flow.Steps)
        {
            builder.Append("\n  await test.step(").Append(Q(step.Description)).Append(", async () => {\n");
            var locator = step.Target is null ? null : Locator(step.Target);
            switch (step.Action)
            {
                case ValidationAction.Navigate: builder.Append("    await page.goto(new URL(").Append(Q(step.Value ?? "/")).Append(", process.env.AGENT_UP_BASE_URL!).toString());\n"); break;
                case ValidationAction.Click: builder.Append("    await ").Append(locator).Append(".click();\n"); break;
                case ValidationAction.Fill: builder.Append("    await ").Append(locator).Append(".fill(").Append(Q(step.Value ?? "")).Append(");\n"); break;
                case ValidationAction.Press: builder.Append("    await page.keyboard.press(").Append(Q(step.Value ?? "Enter")).Append(");\n"); break;
            }
            AppendAssertions(builder, step.Expectations ?? [], "    ");
            builder.Append("  });\n");
        }
        builder.Append("});\n");
        return new PlaywrightExport(Slug(flow.Name) + ".spec.ts", builder.ToString());
    }
    private static void AppendAssertions(StringBuilder b, IReadOnlyList<ValidationAssertion> assertions, string indent)
    {
        foreach (var assertion in assertions)
        {
            var expression = assertion.Kind switch
            {
                ValidationExpectation.Url => assertion.Value.StartsWith('/')
                    ? $"expect(page).toHaveURL(new URL({Q(assertion.Value)}, process.env.AGENT_UP_BASE_URL!).toString())"
                    : $"expect(page).toHaveURL({Q(assertion.Value)})",
                ValidationExpectation.Title => $"expect(page).toHaveTitle({Q(assertion.Value)})",
                ValidationExpectation.Text => $"expect(page.getByText({Q(assertion.Value)}, {{ exact: false }})).toBeVisible()",
                ValidationExpectation.Visible when assertion.Target is not null => $"expect({Locator(assertion.Target)}).toBeVisible()",
                _ => throw new InvalidOperationException("A visible expectation requires a target.")
            };
            b.Append(indent).Append("await ").Append(expression).Append(";\n");
        }
    }
    private static string Locator(ValidationTarget target)
    {
        if (!string.IsNullOrWhiteSpace(target.Role)) return $"page.getByRole({Q(target.Role)}, {{ name: {Q(target.Name ?? target.Text ?? "")} }})";
        if (!string.IsNullOrWhiteSpace(target.Label)) return $"page.getByLabel({Q(target.Label)})";
        if (!string.IsNullOrWhiteSpace(target.TestId)) return $"page.getByTestId({Q(target.TestId)})";
        if (!string.IsNullOrWhiteSpace(target.Text)) return $"page.getByText({Q(target.Text)}, {{ exact: true }})";
        if (!string.IsNullOrWhiteSpace(target.Selector)) return $"page.locator({Q(target.Selector)})";
        throw new InvalidOperationException("An interactive step requires a semantic target or selector.");
    }
    private static string Q(string value) => JsonSerializer.Serialize(value);
    private static string Slug(string value) { var chars = value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray(); return string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries)); }
}
