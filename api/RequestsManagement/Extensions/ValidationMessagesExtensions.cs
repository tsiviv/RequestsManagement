using Microsoft.AspNetCore.Mvc;

namespace RequestsManagement.Extensions;

/// <summary>
/// Hebrew messages for the validation errors produced by ASP.NET itself (model binding, JSON body,
/// the automatic 400 of [ApiController]), so every error the API returns is in the users' language,
/// like the app's own messages. The DTOs' DataAnnotations carry their own Hebrew ErrorMessage.
/// </summary>
public static class ValidationMessagesExtensions
{
    public const string InvalidInputTitle = "הקלט אינו תקין";

    // Used when the JSON body cannot be read; System.Text.Json's own message is not exposed (see below).
    private const string InvalidJsonMessage = "גוף הבקשה אינו JSON תקין, או שאחד הערכים בו אינו מהסוג הנדרש.";

    public static IMvcBuilder AddHebrewValidationMessages(this IMvcBuilder builder)
    {
        builder.AddMvcOptions(options =>
        {
            // Every required DTO field has an explicit [Required] with a Hebrew message. The implicit
            // one (for non-nullable references) only added English duplicates such as
            // "The dto field is required." next to the real error for an unreadable body.
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

        // Do not echo System.Text.Json's exception text (English, and it describes internal types);
        // such errors get InvalidJsonMessage instead.
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
