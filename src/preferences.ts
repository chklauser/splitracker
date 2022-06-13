
export function loadMouseEnabled(): boolean {
  let setting = localStorage.getItem('mouseEnabled');
  return setting === 'true';
}

export function updateMouseEnabled(update: (previousValue: boolean) => boolean): boolean {
  const value = update(loadMouseEnabled());
  localStorage.setItem('mouseEnabled', value.toString());
  return value;
}
