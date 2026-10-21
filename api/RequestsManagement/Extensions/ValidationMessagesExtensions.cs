using Microsoft.AspNetCore.Mvc;

namespace RequestsManagement.Extensions;

// Hebrew messages for ASP.NET's own validation errors (model binding, JSON body, automatic 400).
public static class ValidationMessagesExtensions
{
    public const string InvalidInputTitle = "הקלט אינו תקין";

    private const string InvalidJsonMessage = "גוף הבקשה אינו JSON תקין, או שאחד הערכים בו אינו מהסוג הנדרש.";

    public static IMvcBuilder AddHebrewValidationMessages(this IMvcBuilder builder)
    {
        builder.AddMvcOptions(options =>
        {
            // Required fields have an explicit Hebrew [Required]; the implicit one adds English duplicates.
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

            var messages = options.ModelBindingMessageProvider;
            messages.SetAttemptedValueIsInvalidAccessor((value, field) => $"הערך '{value}' אינו תקין עבור {field}.");
            messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"הערך '{value}' אינו תקין.");
            messages.SetUnknownValueIsInvalidAccessor(field => $"הערך שנשלח עבור {field} אינו תקין.");
            messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => "הערך שנשלח אינו תקין.");
            messages.SetValueIsInvalidAccessor(value => $"הערך '{value}' אינו תקין.");
            messages.SetValueMustBeANumberAccessor(field => $"השדה {field} חייב להיות מספר.");
            messages.SetNonPropertyValueMustBeANumberAccessor(() => "הערך חייב להיות מספר.");
            messages.SetValueMustNotBeNullAccessor(value => $"הערך '{value}' אינו תקין.");
            messages.SetMissingBindRequiredValueAccessor(field => $"חסר ערך עבור {field}.");
            messages.SetMissingKeyOrValueAccessor(() => "חסר ערך.");
            messages.SetMissingRequestBodyRequiredValueAccessor(() => "נדרש גוף בקשה בפורמט JSON.");
        });

        // Do not expose System.Text.Json's exception text (English, describes internal types).
        builder.AddJsonOptions(options => options.AllowInputFormatterExceptionMessages = false);

        builder.ConfigureApiBehaviorOptions(options =>
            options.InvalidModelStateResponseFactory = context =>
            {
                var problem = new ValidationProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = InvalidInputTitle,
                    Instance = context.HttpContext.Request.Path
                };
                foreach (var (field, entry) in context.ModelState)
                {
                    if (entry.Errors.Count > 0)
                    {
                        problem.Errors[field] = entry.Errors
                            .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? InvalidJsonMessage : e.ErrorMessage)
                            .ToArray();
                    }
                }
                return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
            });

        return builder;
    }
}
