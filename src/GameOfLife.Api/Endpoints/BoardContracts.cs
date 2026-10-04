using GameOfLife.Domain;

namespace GameOfLife.Api.Endpoints;

/// <summary>
/// Upload payload. <c>cells</c> is row-major: the outer array is rows.
/// <code>{ "width": 3, "height": 3, "cells": [[false,true,false],[false,true,false],[false,true,false]] }</code>
/// </summary>
/// <remarks>
/// <c>width</c> and <c>height</c> are nullable only so a missing value can be told apart from 0:
/// a missing dimension is an unreadable body, while 0 is a validation failure.
/// </remarks>
public sealed record CreateBoardRequest(int? Width, int? Height, bool[]?[]? Cells);

/// <summary>Board metadata plus generation 0.</summary>
public sealed record BoardResponse(Guid Id, int Width, int Height, int Generation, bool[][] Cells);

/// <summary>A single generation of a board.</summary>
public sealed record GenerationResponse(Guid Id, int Width, int Height, int Generation, bool[][] Cells);

/// <summary>Final state plus the metadata describing how the board concluded.</summary>
/// <param name="Period">1 for a fixed point or extinction, greater than 1 for an oscillator.</param>
/// <param name="GenerationsLimit">The cap the walk used, after the ceiling clamp.</param>
public sealed record FinalStateResponse(
    Guid Id,
    int Width,
    int Height,
    bool[][] Cells,
    TerminationKind TerminationKind,
    int FirstOccurrenceGeneration,
    int Period,
    int GenerationsComputed,
    int GenerationsLimit);
