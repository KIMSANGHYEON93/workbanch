namespace Workbench.Application.DTOs;

/// <summary>
/// 업무 규칙 위반은 예외가 아니라 값으로 돌려준다 — 화면이 오류를 폼 옆에 그대로 보여줘야 하고,
/// 예상 가능한 입력 실수로 스택 트레이스를 남기지 않기 위함이다.
/// (진짜 장애는 예외로 두고 여기서 다루지 않는다.)
/// </summary>
public class OperationResult
{
    protected OperationResult(bool succeeded, IReadOnlyList<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    public bool Succeeded { get; }

    public IReadOnlyList<string> Errors { get; }

    public static OperationResult Success() => new(true, []);

    public static OperationResult Failure(params string[] errors) => new(false, errors);
}

public sealed class OperationResult<TValue> : OperationResult
{
    private OperationResult(bool succeeded, TValue? value, IReadOnlyList<string> errors)
        : base(succeeded, errors)
    {
        Value = value;
    }

    public TValue? Value { get; }

    public static OperationResult<TValue> Success(TValue value) => new(true, value, []);

    public static new OperationResult<TValue> Failure(params string[] errors) => new(false, default, errors);
}
