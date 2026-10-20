using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using RequestsManagement.Services;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RequestsManagement.Extensions;

/// <summary>
/// Shows the X-User-Name header on every changing operation in Swagger UI. The header is read by
/// HeaderCurrentUser (not bound to an action parameter), so Swagger would otherwise not know about
/// it and "Try it out" on a status update would just return 400.
/// </summary>
public sealed class UserNameHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.HttpMethod is null or "GET" or "HEAD")
        {
            return;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = HeaderCurrentUser.HeaderName,
            In = ParameterLocation.Header,
            Required = true,
            Description = "שם המשתמש שמבצע את השינוי (נרשם כ-ChangedBy בהיסטוריה). " +
                          "UTF-8 בקידוד percent-encoding, למשל %D7%93%D7%A0%D7%94 עבור \"דנה\".",
            Schema = new OpenApiSchema { Type = "string", Default = new OpenApiString("swagger-user") }
        });
    }
}
