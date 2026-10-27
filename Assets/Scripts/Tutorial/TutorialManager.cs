
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    [Serializable]
    public class TutorialStep
    {
        public Transform targetObject;

        [Tooltip("Outline used as the highlight")]
        public Outline highlight;

        [TextArea(2, 5)]
        public string instruction;

        [Tooltip("World-space offset from the target")]
        public Vector3 panelOffset = new Vector3(0, 0.3f, 0);
    }

    [Header("Instruction UI")]
    [SerializeField] private GameObject instructionPanel;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private Button nextButton;

    [Header("Tutorial Settings")]
    [SerializeField] private TutorialStep[] steps;

    private int currentStepIndex = -1;

    private void Start()
    {
        // Connect the Next button automatically.
        //nextButton.onClick.AddListener(NextStep);

        // Make sure no highlights are visible initially.
        HideAllHighlights();
        instructionPanel.SetActive(false);

        StartTutorial();
    }

    private void LateUpdate()
    {
        // Keep the instruction panel near the current target
        // even if the target moves.
        if (currentStepIndex >= 0 &&
            currentStepIndex < steps.Length)
        {
            UpdatePanelPosition();
        }
    }

    public void StartTutorial()
    {
        HideAllHighlights();

        if (steps == null || steps.Length == 0)
            return;

        currentStepIndex = 0;
        ShowStep(currentStepIndex);
    }

    private void ShowStep(int index)
    {
        TutorialStep step = steps[index];

        // Show the instruction panel.
        instructionPanel.SetActive(true);

        // Update instruction text.
        instructionText.text = step.instruction;

        // Activate this step's highlight.
        if (step.highlight != null)
        {
            step.highlight.enabled=true;
        }

        // Move the instruction panel near the target.
        UpdatePanelPosition();
    }

    private void UpdatePanelPosition()
    {
        TutorialStep step = steps[currentStepIndex];

        if (step.targetObject != null)
        {
            instructionPanel.transform.position =
                step.targetObject.position + step.panelOffset;
        }
    }

    public void NextStep()
    {
        if (currentStepIndex < 0)
            return;

        // Hide the current highlight.
        TutorialStep currentStep = steps[currentStepIndex];

        if (currentStep.highlight != null)
        {
            currentStep.highlight.enabled = false;
        }

        // Advance to the next step.
        currentStepIndex++;

        if (currentStepIndex < steps.Length)
        {
            ShowStep(currentStepIndex);
        }
        else
        {
            EndTutorial();
        }
    }

    public void EndTutorial()
    {
        HideAllHighlights();
        instructionPanel.SetActive(false);
        currentStepIndex = -1;
    }

    private void HideAllHighlights()
    {
        if (steps == null)
            return;

        foreach (TutorialStep step in steps)
        {
            if (step.highlight != null)
            {
                step.highlight.enabled = false;
            }
        }
    }

    private void OnDestroy()
    {
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(NextStep);
        }
    }
}
