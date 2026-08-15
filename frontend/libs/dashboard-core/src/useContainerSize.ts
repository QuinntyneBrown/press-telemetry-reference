import { useCallback, useRef, useState } from 'react';

export interface ContainerSize {
  width: number;
  height: number;
}

/** Observes an element's content box via ResizeObserver (L2-016 AC4 chart redraw). */
export function useContainerSize<T extends HTMLElement>(): {
  ref: (element: T | null) => void;
  size: ContainerSize;
} {
  const [size, setSize] = useState<ContainerSize>({ width: 0, height: 0 });
  const observerRef = useRef<ResizeObserver | null>(null);

  const ref = useCallback((element: T | null) => {
    observerRef.current?.disconnect();
    observerRef.current = null;
    if (!element) return;
    const observer = new ResizeObserver(entries => {
      const rect = entries[0]?.contentRect;
      if (rect) setSize({ width: rect.width, height: rect.height });
    });
    observer.observe(element);
    observerRef.current = observer;
  }, []);

  return { ref, size };
}
