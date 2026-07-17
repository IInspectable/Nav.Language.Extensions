#region Using Directives

using NavV2Demo.IWFL;

#endregion

namespace NavV2Demo.WFL {
    // Handgeschriebener Logic-Teil des Demo-Workflows — das Testobjekt für die Begin↔After-Navigation.
    // Jede Override delegiert über den generierten CallContext (Parameter `next`) an genau die Ausgänge,
    // die Demo.nav für den jeweiligen Knoten vorsieht. Der Schwerpunkt: die verschachtelte Choice
    // ChoiceSaveChanges → ChoiceValidation, über die die Continuation (die modale Fehler-Box) fließt.
    public partial class ContinuationFlowWFS {
        // ── Demo-Zustand ──────────────────────────────────────────────────────────────────────────
        // In einem echten WFS käme das aus dem Datenmodell bzw. der View; hier reine Kompilier-Attrappe.
        static bool IsValid(string subject) => !string.IsNullOrWhiteSpace(subject);
        bool HasPendingChanges() => true;

        // init Init1 --> ChoiceValidation: beim Start erst prüfen, dann anzeigen.
        protected override Init1CallContext.Result BeginLogic(Init1CallContext next) {
            return next.ChoiceValidation(subject: "Start");
        }

        // choice ChoiceValidation: gültig zeigt nur die View, ungültig legt modal die Fehler-Box darüber.
        protected override ChoiceValidationCallContext.Result ChoiceValidationLogic(string subject,
                                                                                    ChoiceValidationCallContext next) {
            var to = new DemoViewTO();

            if (IsValid(subject)) {
                return next.ShowDemoView(to);
            }

            // Continuation: DemoView zeigen UND modal die Fehler-Box darüberlegen (GotoGUI.Concat(OpenModalTask)).
            return next.ShowDemoView(to)
                       .BeginError($"Ungültige Eingabe: '{subject}'.");
        }

        // trigger OnSave --> ChoiceSaveChanges: nicht direkt prüfen, sondern über die Save-Choice gehen.
        protected override OnSaveCallContext.Result OnSaveLogic(DemoViewTO to,
                                                                OnSaveCallContext next) {
            return next.ChoiceSaveChanges();
        }

        // choice ChoiceSaveChanges: ohne Änderungen sofort raus, mit Änderungen weiter in die Prüfung.
        protected override ChoiceSaveChangesCallContext.Result ChoiceSaveChangesLogic(ChoiceSaveChangesCallContext next) {
            if (!HasPendingChanges()) {
                return next.Exit(par: true);
            }

            // Verschachtelte Choice: vor dem Speichern erneut prüfen — über diesen Sprung fließt die Continuation.
            return next.ChoiceValidation(subject: "Speichern");
        }

        // trigger OnRefresh --> ChoiceValidation: erneut prüfen (z.B. nach Nachbearbeitung).
        protected override OnRefreshCallContext.Result OnRefreshLogic(DemoViewTO to,
                                                                      OnRefreshCallContext next) {
            return next.ChoiceValidation(subject: "Aktualisieren");
        }

        // trigger OnClose --> Exit: schließen ohne Speichern.
        protected override OnCloseCallContext.Result OnCloseLogic(DemoViewTO to,
                                                                  OnCloseCallContext next) {
            return next.Exit(par: false);
        }

        // exit Error:Ok --> DemoView: Rücksprung aus der modalen Fehler-Box zurück auf die View.
        protected override AfterErrorCallContext.Result AfterErrorLogic(ErrorBoxResult result,
                                                                        AfterErrorCallContext next) {
            if (result == ErrorBoxResult.Abbrechen) {
                return next.Cancel();
            }

            return next.ShowDemoView(new DemoViewTO());
        }
    }
}
