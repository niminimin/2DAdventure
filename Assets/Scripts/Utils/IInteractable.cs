
public interface IInteractable 
{
    public void TriggerAction();

    /// <summary>现在还能不能交互（例如宝箱打开后就不能了）。</summary>
    public bool CanInteract { get; }
}
