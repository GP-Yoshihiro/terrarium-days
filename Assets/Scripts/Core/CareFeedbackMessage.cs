namespace TerrariumDays.Core
{
    /// <summary>
    /// Picks the care-action feedback message: the action's own success message, unless
    /// the target stat was already at maximum before the tap. See 仕様書 section 9.2.
    /// </summary>
    public static class CareFeedbackMessage
    {
        public const string AlreadyFullMessage = "もう十分だよ";

        public static string For(double valueBeforeAction, string successMessage)
        {
            return valueBeforeAction >= StatusValue.Maximum ? AlreadyFullMessage : successMessage;
        }
    }
}
