namespace AgentUp.Server.Features.Training.DTOs;

/// <summary>One category a decision can be filed under, and what belongs in it.</summary>
public sealed record DecisionCategoryDto(string Id, string Label, string Covers);
