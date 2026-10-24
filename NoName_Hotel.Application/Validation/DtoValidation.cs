using System.ComponentModel.DataAnnotations;
using NoName_Hotel.Common.Constants;
using NoName_Hotel.Common.Results;
namespace NoName_Hotel.Application.Validation;
public static class DtoValidation
{
    public static Error[] Errors(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, true);
        return results.Select(r => new Error(ErrorCodes.Validation, r.ErrorMessage ?? "Invalid input.")).ToArray();
    }
}
