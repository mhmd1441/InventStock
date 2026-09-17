const currency = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
});

const number = new Intl.NumberFormat("en-US");

const dateTime = new Intl.DateTimeFormat("en-GB", {
  dateStyle: "medium",
  timeStyle: "short",
});

export const formatCurrency = (value: number) => currency.format(value);

export const formatNumber = (value: number) => number.format(value);

export function formatDateTime(value: string) {
  // Every timestamp from the API is UTC; add the suffix back if it was serialised without one,
  // otherwise the browser would read it as local time.
  const normalised = /[Z+-]\d{2}:?\d{2}$|Z$/.test(value) ? value : `${value}Z`;
  return dateTime.format(new Date(normalised));
}

export function formatSignedQuantity(value: number) {
  return value > 0 ? `+${number.format(value)}` : number.format(value);
}
