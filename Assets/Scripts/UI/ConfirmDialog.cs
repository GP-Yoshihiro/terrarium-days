using System;
using UnityEngine.UIElements;

namespace TerrariumDays.UI
{
    /// <summary>
    /// A yes/no confirmation modal (§9): purchases and wholesale trades ask before they take
    /// effect. Owns no domain logic — the caller supplies the message and the action to run
    /// on "はい"; "いいえ" (or Hide()) just closes it.
    /// </summary>
    public sealed class ConfirmDialog
    {
        private readonly VisualElement modal;
        private readonly Label messageLabel;
        private readonly Button yesButton;
        private readonly Button noButton;
        private Action onYes;

        public ConfirmDialog(VisualElement modal, Label messageLabel, Button yesButton, Button noButton)
        {
            this.modal = modal;
            this.messageLabel = messageLabel;
            this.yesButton = yesButton;
            this.noButton = noButton;

            if (this.yesButton != null)
            {
                this.yesButton.clicked += OnYesClicked;
            }

            if (this.noButton != null)
            {
                this.noButton.clicked += OnNoClicked;
            }

            SetDisplay(false);
        }

        public bool IsOpen { get; private set; }

        public void Show(string message, Action onYesAction)
        {
            onYes = onYesAction;
            if (messageLabel != null)
            {
                messageLabel.text = message;
            }

            IsOpen = true;
            SetDisplay(true);
        }

        public void Hide()
        {
            IsOpen = false;
            onYes = null;
            SetDisplay(false);
        }

        /// <summary>Unhooks the yes/no button callbacks; call from the owner's OnDestroy.</summary>
        public void Dispose()
        {
            if (yesButton != null)
            {
                yesButton.clicked -= OnYesClicked;
            }

            if (noButton != null)
            {
                noButton.clicked -= OnNoClicked;
            }
        }

        private void OnYesClicked()
        {
            var action = onYes;
            Hide();
            action?.Invoke();
        }

        private void OnNoClicked()
        {
            Hide();
        }

        private void SetDisplay(bool visible)
        {
            if (modal != null)
            {
                modal.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
