namespace AgentUp.AUDebug.Features.Validation.Interfaces;

public interface IValidationConfigFile
{
    bool Exists(string path);

    string ReadAllText(string path);
}
