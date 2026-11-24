# Context and alternatives

[Azure Machine Learning model monitoring](https://learn.microsoft.com/en-us/azure/machine-learning/concept-model-monitoring?view=azureml-api-2) already provides managed distribution metrics and data quality signals. It compares model inputs against a reference such as training data and offers metrics beyond this tool. Managed monitoring is broader than a local file comparison.

Feature Shift is a small offline C# workflow for descriptive summaries of two named-numeric JSONL files, including the feature layout used by [Ridge Train Kit](https://github.com/alirezaRanjbar4/ridge-train-kit). No measured external demand or uniqueness is claimed. Its standardized mean difference is a defined asymmetric descriptive ratio, not a significance test or equivalent to PSI, KS, Wasserstein distance or Azure's monitoring metrics. It does not fit a model, compute accuracy or establish that training and candidate records are independent. See [design](DESIGN.md).
