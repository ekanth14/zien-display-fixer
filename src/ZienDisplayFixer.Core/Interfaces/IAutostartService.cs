namespace ZienDisplayFixer.Core.Interfaces;

public interface IAutostartService
{
    bool IsAutostartEnabled();
    void SetAutostart(bool enable);
}
