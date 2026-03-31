namespace Fluxorq.Abstractions;

/// <summary>
/// Represents the next step in the pipeline — either the next behavior or the final handler.
/// Call this delegate to continue execution; skip it to short-circuit the pipeline.
/// </summary>
/// <typeparam name="TResponse">The response type this pipeline step will eventually produce.</typeparam>
public delegate Task<TResponse> NextDelegate<TResponse>();
