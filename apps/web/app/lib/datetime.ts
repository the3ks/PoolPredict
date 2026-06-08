export function formatDisplayDateTime(value: string | number | Date) {
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "";
  }

  const day = pad(date.getDate());
  const month = pad(date.getMonth() + 1);
  const hour24 = date.getHours();
  const hour12 = hour24 % 12 || 12;
  const minute = pad(date.getMinutes());
  const meridiem = hour24 >= 12 ? "PM" : "AM";
  return `${day}/${month} ${pad(hour12)}:${minute} ${meridiem}`;
}

export function formatDisplayDate(value: string) {
  return formatDisplayDateTime(`${value}T00:00:00`);
}

function pad(value: number) {
  return value.toString().padStart(2, "0");
}
