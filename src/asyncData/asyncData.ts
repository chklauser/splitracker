import {useCallback, useEffect, useState} from "react";

export const enum PromiseState {
  Pending = 'pending',
  Resolved = 'resolved',
  Rejected = 'rejected',
}

export interface UnknownStatePromiseData<T> {
  reload: () => void;
}

export interface PendingPromiseData<T> {
  data: undefined;
  state: PromiseState.Pending;
  isPending: true;
  isResolved: false;
  isRejected: false;
  isSettled: false;
  error: undefined;
}

export interface RejectedPromiseData<T> {
  data: undefined;
  state: PromiseState.Rejected;
  isPending: false;
  isResolved: false;
  isRejected: true;
  isSettled: true;
  error: any;
}

export interface ResolvedPromiseData<T> {
  data: T;
  state: PromiseState.Resolved;
  isPending: false;
  isResolved: true;
  isRejected: false;
  isSettled: true;
  error: undefined;
}

export type PromiseData<T> = UnknownStatePromiseData<T> & (
  | PendingPromiseData<T>
  | RejectedPromiseData<T>
  | ResolvedPromiseData<T>
  );

export function usePromiseFn<T>(promiseFn: (...args: any) => Promise<T>, args: [...Parameters<typeof promiseFn>]): PromiseData<T> {
  const [scheduled, setScheduled] = useState(false);
  const [data, setData] = useState<T | undefined>(undefined);
  const [state, setState] = useState<PromiseState>(PromiseState.Pending);
  const [error, setError] = useState<any | undefined>(undefined);
  const [isPending, setIsPending] = useState(true);
  const [isResolved, setIsResolved] = useState(false);
  const [isRejected, setIsRejected] = useState(false);
  const [isSettled, setIsSettled] = useState(false);
  const reload = useCallback(() => {
    const doReload = async () => {
      if(!scheduled) {
        setState(PromiseState.Pending);
        setIsPending(true);
        setIsResolved(false);
        setIsRejected(false);
        setIsSettled(false);
        setData(undefined);
      } else {
        setScheduled(true);
      }
      try {
        const value = await promiseFn(...args);
        console.log("Resolved promise.", value, "args", args);
        setData(value);
        setState(PromiseState.Resolved);
        setIsPending(false);
        setIsResolved(true);
        setIsRejected(false);
        setIsSettled(true);
      } catch (reason) {
        setData(undefined);
        setState(PromiseState.Rejected);
        setIsPending(false);
        setIsResolved(false);
        setIsRejected(true);
        setIsSettled(true);
        setError(reason);
      }
    }
    doReload().catch(console.error);
  }, [...args]);
  useEffect(() => {
    reload();
  }, [reload]);
  return {
    data,
    state,
    isPending,
    isResolved,
    isRejected,
    isSettled,
    reload,
    error,
  } as PromiseData<T>;
}

export function useDeferredFn<F extends (...args: [...B,...L]) => Promise<void>, B extends unknown[], L extends unknown[]>(
  fn: (...args: any) => Promise<void>, closureArgs: B
): ((...args: [...L]) => void) {
  return (...args) => {
    fn(...closureArgs, ...args).catch(console.error);
  }
}
