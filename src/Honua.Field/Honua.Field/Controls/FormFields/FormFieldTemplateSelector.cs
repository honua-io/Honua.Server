using HonuaField.Models.FormBuilder;

namespace HonuaField.Controls.FormFields;

/// <summary>
/// Template selector for dynamically choosing the appropriate form field control
/// based on the field type
/// </summary>
public class FormFieldTemplateSelector : DataTemplateSelector
{
	/// <summary>
	/// Template for text form fields
	/// </summary>
	public DataTemplate? TextFieldTemplate { get; set; }

	/// <summary>
	/// Template for multiline text form fields
	/// </summary>
	public DataTemplate? MultilineTextFieldTemplate { get; set; }

	/// <summary>
	/// Template for number form fields
	/// </summary>
	public DataTemplate? NumberFieldTemplate { get; set; }

	/// <summary>
	/// Template for boolean form fields
	/// </summary>
	public DataTemplate? BooleanFieldTemplate { get; set; }

	/// <summary>
	/// Template for choice form fields
	/// </summary>
	public DataTemplate? ChoiceFieldTemplate { get; set; }

	/// <summary>
	/// Template for date form fields
	/// </summary>
	public DataTemplate? DateFieldTemplate { get; set; }

	/// <summary>
	/// Template for datetime form fields
	/// </summary>
	public DataTemplate? DateTimeFieldTemplate { get; set; }

	/// <summary>
	/// Select the appropriate template based on the form field type
	/// </summary>
	protected override DataTemplate? OnSelectTemplate(object item, BindableObject container)
	{
		if (item is FormField field)
		{
			return field switch
			{
				TextFormField => TextFieldTemplate,
				MultilineTextFormField => MultilineTextFieldTemplate,
				NumberFormField => NumberFieldTemplate,
				BooleanFormField => BooleanFieldTemplate,
				ChoiceFormField => ChoiceFieldTemplate,
				DateFormField => DateFieldTemplate,
				DateTimeFormField => DateTimeFieldTemplate,
				_ => TextFieldTemplate // Default to text field
			};
		}

		return TextFieldTemplate;
	}
}
