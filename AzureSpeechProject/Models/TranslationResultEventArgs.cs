namespace AzureSpeechProject.Models;

public sealed class TranslationResultEventArgs : EventArgs
{
    public TranslationResult Result { get; }

    public TranslationResultEventArgs(TranslationResult result)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }
}
