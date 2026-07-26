using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "OfficeComputerWorkdayDefinition",
    menuName = "Office/Computer/Workday Definition")]
public sealed class OfficeComputerWorkdayDefinition : ScriptableObject
{
    [Header("Workday")]
    [Tooltip("仅用于配置识别和备注。内部存档实际使用每个案件上的 progressId。")]
    [SerializeField] private string workdayId = "Office.Computer.Workday.One";

    [SerializeField] private string completedTitle = "Workday Complete";

    [TextArea(3, 8)]
    [SerializeField] private string completedBody =
        "All scheduled cases have been filed. Reopen the terminal to review the workday.";

    [Header("Cases")]
    [SerializeField] private List<OfficeComputerCaseDefinition> cases =
        new List<OfficeComputerCaseDefinition>();

    public string WorkdayId => workdayId;
    public string CompletedTitle => completedTitle;
    public string CompletedBody => completedBody;
    public IReadOnlyList<OfficeComputerCaseDefinition> Cases => cases;
}

[Serializable]
public sealed class OfficeComputerCaseDefinition
{
    [Header("Identity")]
    [Tooltip("稳定的案件标识。已经进入存档的 ID 不要改名。")]
    [SerializeField] private string caseId = "ExpenseApproval";

    [Header("Persistent Objectives")]
    [Tooltip("首次阅读邮件时写入 Completion。")]
    [SerializeField] private string mailProgressId;

    [Tooltip("首次查看证据文件时写入 Completion。")]
    [SerializeField] private string documentsProgressId;

    [Tooltip("选择正确处理方式时写入 Completion，代表该案件完成。")]
    [SerializeField] private string taskProgressId;

    [Tooltip("可选。每次提交错误处理方式时增加 Open 次数，供以后评分或剧情使用。")]
    [SerializeField] private string incorrectDecisionProgressId;

    [Header("Mail")]
    [SerializeField] private string mailTitle = "New Mail";

    [TextArea(5, 12)]
    [SerializeField] private string mailBody;

    [Header("Documents")]
    [SerializeField] private string documentsTitle = "Case Documents";

    [TextArea(5, 12)]
    [SerializeField] private string documentsBody;

    [Header("Task")]
    [SerializeField] private string taskTitle = "Choose a Handling Method";

    [TextArea(3, 8)]
    [SerializeField] private string taskBody;

    [TextArea(2, 5)]
    [SerializeField] private string taskLockedMessage =
        "Read the mail and review the attached documents before submitting a decision.";

    [SerializeField] private List<OfficeComputerDecisionDefinition> decisions =
        new List<OfficeComputerDecisionDefinition>();

    public string CaseId => caseId;
    public string MailProgressId => mailProgressId;
    public string DocumentsProgressId => documentsProgressId;
    public string TaskProgressId => taskProgressId;
    public string IncorrectDecisionProgressId => incorrectDecisionProgressId;
    public string MailTitle => mailTitle;
    public string MailBody => mailBody;
    public string DocumentsTitle => documentsTitle;
    public string DocumentsBody => documentsBody;
    public string TaskTitle => taskTitle;
    public string TaskBody => taskBody;
    public string TaskLockedMessage => taskLockedMessage;
    public IReadOnlyList<OfficeComputerDecisionDefinition> Decisions => decisions;
}

[Serializable]
public sealed class OfficeComputerDecisionDefinition
{
    [SerializeField] private string label = "Decision";

    [Tooltip("这个决定是否能完成当前案件。每个案件应该只配置一个正确决定。")]
    [SerializeField] private bool isCorrect;

    [TextArea(2, 5)]
    [SerializeField] private string feedback;

    public string Label => label;
    public bool IsCorrect => isCorrect;
    public string Feedback => feedback;
}
