using System.Collections.Generic;
using System.Text;
using IngameDebugConsole;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    public class ShowHelp : MonoBehaviour
    {
        [SerializeField] private Button showBtn;
        [SerializeField] private Button hideBtn;
        [SerializeField] private Text helpText;
        [SerializeField] private GameObject panel;
        
        public List<string> Notes { get; } = new();

        private void Awake()
        {
            showBtn.onClick.AddListener(ShowHelpPanel);
            hideBtn.onClick.AddListener(HideHelpPanel);
        }

        private void ShowHelpPanel()
        {
            helpText.text = BuildHelpText();
            panel.SetActive(true);
        }

        private void HideHelpPanel()
        {
            panel.SetActive(false);
        }
        
        string BuildHelpText()
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.Append("- <b>Dev note:</b>");

            foreach (var note in Notes)
            {
                stringBuilder.Append("\n\t+ ").Append(note);
            }
            
            var commands = DebugLogConsole.GetAllCommands();
            
            stringBuilder.Append("\n\n\n\n- <b>Available commands:</b>");

            foreach (var command in commands)
            {
                if (command.IsValid())
                    stringBuilder.Append("\n    - ").Append(command.signature);
            }

            return stringBuilder.ToString();
        }
    }

}