using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace SatbayevUniversity.Syllabus.Infrastructure.Services;

public static class EnumExtensions
{
    public static string DisplayName(this Enum value) =>
        value.GetType().GetMember(value.ToString()).FirstOrDefault()?
            .GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
}
