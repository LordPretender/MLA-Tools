namespace MlaTools;

/// <summary>Base des dialogues de saisie à validation OK / Annuler : réglages de fenêtre et barre de boutons communs.</summary>
internal class FormDialogue : FormBase
{
    protected FormDialogue(string titre, Size tailleClient)
    {
        Text = titre;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = tailleClient;
        MinimumSize = Size;
    }

    protected FlowLayoutPanel ConstruireBoutons(Action valider)
    {
        var ok = new Button { Text = "OK", AutoSize = true };
        var annuler = new Button { Text = "Annuler", AutoSize = true, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => valider();
        AcceptButton = ok;
        CancelButton = annuler;
        return OutilsDialogue.BarreBoutons(annuler, ok);
    }
}
