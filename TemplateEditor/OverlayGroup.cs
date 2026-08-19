using System;
using System.Collections.Generic;

namespace TemplateEditor;

internal sealed class OverlayGroup : IDisposable
{
	private readonly IReadOnlyList<IDisposable> _overlays;

	public OverlayGroup(IReadOnlyList<IDisposable> overlays)
	{
		_overlays = overlays ?? new List<IDisposable>();
	}

	public void Dispose()
	{
		foreach (IDisposable overlay in _overlays)
		{
			overlay?.Dispose();
		}
	}
}
