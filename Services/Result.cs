namespace RideSync.Web.Services;

public enum Outcome { Ok, NotFound, Conflict, Forbidden, Busy, Invalid }

/// Services never touch HTTP: they return an Outcome and the controller decides how to show it (MVC separation).
public record Result<T>(Outcome Outcome, T? Value = default, string? Error = null)
{
    public bool Ok => Outcome == Outcome.Ok;
    public static Result<T> Success(T v) => new(Outcome.Ok, v);
    public static Result<T> Fail(Outcome o, string msg) => new(o, default, msg);
}
