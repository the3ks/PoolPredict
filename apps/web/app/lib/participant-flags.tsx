import { ReactNode } from "react";

const participantCodeToCountryCode: Record<string, string> = {
  AFG: "AF",
  ALB: "AL",
  ALG: "DZ",
  AND: "AD",
  ANG: "AO",
  ARG: "AR",
  ARM: "AM",
  AUS: "AU",
  AUT: "AT",
  AZE: "AZ",
  BAH: "BS",
  BAN: "BD",
  BEL: "BE",
  BEN: "BJ",
  BFA: "BF",
  BHR: "BH",
  BIH: "BA",
  BLR: "BY",
  BOL: "BO",
  BRA: "BR",
  BUL: "BG",
  CAN: "CA",
  CHI: "CL",
  CHN: "CN",
  CIV: "CI",
  CMR: "CM",
  COD: "CD",
  COL: "CO",
  CRC: "CR",
  CRO: "HR",
  CZE: "CZ",
  DEN: "DK",
  ECU: "EC",
  EGY: "EG",
  ENG: "GB",
  ESP: "ES",
  FIN: "FI",
  FRA: "FR",
  GAB: "GA",
  GEO: "GE",
  GER: "DE",
  GHA: "GH",
  GRE: "GR",
  GUI: "GN",
  HON: "HN",
  HUN: "HU",
  IDN: "ID",
  IND: "IN",
  IRL: "IE",
  IRN: "IR",
  IRQ: "IQ",
  ISL: "IS",
  ISR: "IL",
  ITA: "IT",
  JAM: "JM",
  JPN: "JP",
  KOR: "KR",
  KSA: "SA",
  KUW: "KW",
  LBN: "LB",
  MEX: "MX",
  MLI: "ML",
  MAR: "MA",
  NED: "NL",
  NGA: "NG",
  NIR: "GB",
  NOR: "NO",
  NZL: "NZ",
  PAN: "PA",
  PAR: "PY",
  PER: "PE",
  POL: "PL",
  POR: "PT",
  QAT: "QA",
  ROU: "RO",
  RSA: "ZA",
  RUS: "RU",
  SCO: "GB",
  SEN: "SN",
  SRB: "RS",
  SUI: "CH",
  SVK: "SK",
  SVN: "SI",
  SWE: "SE",
  THA: "TH",
  TUN: "TN",
  TUR: "TR",
  UAE: "AE",
  UKR: "UA",
  URU: "UY",
  USA: "US",
  VEN: "VE",
  VIE: "VN",
  WAL: "GB",
};

export function ParticipantName({
  code,
  name,
}: {
  code?: string | null;
  name: string;
}) {
  const countryCode = getCountryCodeFromParticipantCode(code);
  if (!countryCode) {
    return <>{name}</>;
  }

  return (
    <span className="participantName">
      <img
        alt=""
        aria-hidden="true"
        className="participantFlag"
        height={14}
        loading="lazy"
        src={`https://flagcdn.com/w40/${countryCode.toLowerCase()}.png`}
        width={20}
      />
      <span>{name}</span>
    </span>
  );
}

export function MatchName({
  awayCode,
  awayName,
  homeCode,
  homeName,
}: {
  awayCode?: string | null;
  awayName: string;
  homeCode?: string | null;
  homeName: string;
}) {
  return (
    <>
      <ParticipantName code={homeCode} name={homeName} />
      {" -vs- "}
      <ParticipantName code={awayCode} name={awayName} />
    </>
  );
}

export function formatParticipantName(name: string, code?: string | null) {
  return name;
}

export function formatMatchNameText({
  awayCode,
  awayName,
  homeCode,
  homeName,
}: {
  awayCode?: string | null;
  awayName: string;
  homeCode?: string | null;
  homeName: string;
}) {
  return `${formatParticipantName(homeName, homeCode)} -vs- ${formatParticipantName(awayName, awayCode)}`;
}

function getCountryCodeFromParticipantCode(code?: string | null) {
  if (!code) {
    return "";
  }

  return participantCodeToCountryCode[code.trim().toUpperCase()] ?? "";
}

export type ParticipantLabel = ReactNode;
