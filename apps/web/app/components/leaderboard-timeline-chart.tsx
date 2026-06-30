import type { LeaderboardTimeline } from "../lib/types";

const chartColors = [
  "#46d48c",
  "#f4c95d",
  "#7dd3fc",
  "#f472b6",
  "#c4b5fd",
  "#fb7185",
  "#a3e635",
  "#fdba74",
];

export function LeaderboardTimelineChart({
  timeline,
}: {
  timeline: LeaderboardTimeline | null;
}) {
  if (!timeline || timeline.members.length === 0) {
    return <p className="mutedText">No ranked members to chart yet.</p>;
  }

  if (timeline.events.length === 0) {
    return <p className="mutedText">No settled events to chart yet.</p>;
  }

  const width = 960;
  const height = 420;
  const padding = { top: 24, right: 24, bottom: 56, left: 72 };
  const plotWidth = width - padding.left - padding.right;
  const plotHeight = height - padding.top - padding.bottom;
  const values = timeline.events.flatMap((event) =>
    event.points.map((point) => point.winLoss),
  );
  const minValue = Math.min(0, ...values);
  const maxValue = Math.max(0, ...values);
  const range = maxValue - minValue || 1;
  const xForIndex = (index: number) =>
    padding.left +
    (timeline.events.length === 1
      ? plotWidth / 2
      : (index / (timeline.events.length - 1)) * plotWidth);
  const yForValue = (value: number) =>
    padding.top + ((maxValue - value) / range) * plotHeight;
  const zeroY = yForValue(0);
  const yTicks = buildTicks(minValue, maxValue);

  return (
    <div className="leaderboardTimelineChart">
      <div className="leaderboardTimelineScroller">
        <svg
          aria-label="Ranked member WinLoss trend by settled event"
          className="leaderboardTimelineSvg"
          role="img"
          viewBox={`0 0 ${width} ${height}`}
        >
          <line
            className="leaderboardTimelineAxis"
            x1={padding.left}
            x2={padding.left + plotWidth}
            y1={zeroY}
            y2={zeroY}
          />
          {yTicks.map((tick) => (
            <g key={tick}>
              <line
                className="leaderboardTimelineGridline"
                x1={padding.left}
                x2={padding.left + plotWidth}
                y1={yForValue(tick)}
                y2={yForValue(tick)}
              />
              <text
                className="leaderboardTimelineTick"
                x={padding.left - 10}
                y={yForValue(tick) + 4}
                textAnchor="end"
              >
                {formatNumber(tick)}
              </text>
            </g>
          ))}
          {timeline.events.map((event, index) => (
            <g key={event.eventId}>
              <line
                className="leaderboardTimelineEventLine"
                x1={xForIndex(index)}
                x2={xForIndex(index)}
                y1={padding.top}
                y2={padding.top + plotHeight}
              />
              <text
                className="leaderboardTimelineEventTick"
                x={xForIndex(index)}
                y={height - 26}
                textAnchor="middle"
              >
                E{event.sequence}
              </text>
            </g>
          ))}
          {timeline.members.map((member, memberIndex) => {
            const color = chartColors[memberIndex % chartColors.length];
            const points = timeline.events.map((event, eventIndex) => {
              const point = event.points.find(
                (candidate) => candidate.memberId === member.memberId,
              );
              return {
                x: xForIndex(eventIndex),
                y: yForValue(point?.winLoss ?? 0),
                value: point?.winLoss ?? 0,
                eventName: event.eventName,
              };
            });

            return (
              <g key={member.memberId}>
                <polyline
                  fill="none"
                  points={points.map((point) => `${point.x},${point.y}`).join(" ")}
                  stroke={color}
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth="3"
                />
                {points.map((point, pointIndex) => (
                  <circle
                    cx={point.x}
                    cy={point.y}
                    fill={color}
                    key={`${member.memberId}-${pointIndex}`}
                    r="4"
                  >
                    <title>
                      {member.displayName} | {point.eventName} | WinLoss{" "}
                      {formatNumber(point.value)}
                    </title>
                  </circle>
                ))}
              </g>
            );
          })}
        </svg>
      </div>
      <div className="leaderboardTimelineLegend">
        {timeline.members.map((member, index) => (
          <span className="leaderboardTimelineLegendItem" key={member.memberId}>
            <span
              aria-hidden="true"
              className="leaderboardTimelineLegendSwatch"
              style={{ backgroundColor: chartColors[index % chartColors.length] }}
            />
            <span>{member.displayName}</span>
            <strong>{formatNumber(member.currentWinLoss)}</strong>
          </span>
        ))}
      </div>
      <details className="leaderboardTimelineEvents">
        <summary>Events</summary>
        {timeline.events.map((event) => (
          <span key={event.eventId}>
            E{event.sequence}: {event.eventName}
          </span>
        ))}
      </details>
    </div>
  );
}

function buildTicks(minValue: number, maxValue: number) {
  if (minValue === maxValue) {
    return [minValue];
  }

  const tickCount = 5;
  const step = (maxValue - minValue) / (tickCount - 1);
  return Array.from({ length: tickCount }, (_, index) =>
    Math.round(minValue + step * index),
  );
}

function formatNumber(value: number) {
  return new Intl.NumberFormat("en-US", {
    maximumFractionDigits: 0,
  }).format(value);
}
