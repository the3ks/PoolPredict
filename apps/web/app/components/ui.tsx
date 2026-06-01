import { ComponentType, ReactNode } from "react";
import type { LucideProps } from "lucide-react";

type IconComponent = ComponentType<LucideProps>;

export function cn(...classes: Array<string | false | null | undefined>) {
  return classes.filter(Boolean).join(" ");
}

export function IconLabel({ children, icon: Icon }: { children: ReactNode; icon: IconComponent }) {
  return (
    <span className="iconLabel">
      <Icon aria-hidden="true" size={18} strokeWidth={2.2} />
      <span>{children}</span>
    </span>
  );
}

export function PageHeader({
  eyebrow,
  title,
  actions,
  icon: Icon
}: {
  eyebrow: string;
  title: ReactNode;
  actions?: ReactNode;
  icon?: IconComponent;
}) {
  return (
    <div className="pageHeader">
      <div className="pageTitleGroup">
        {Icon ? (
          <span className="pageIcon">
            <Icon aria-hidden="true" size={22} strokeWidth={2.2} />
          </span>
        ) : null}
        <div>
          <p className="eyebrow">{eyebrow}</p>
          <h1>{title}</h1>
        </div>
      </div>
      {actions ? <div className="buttonRow">{actions}</div> : null}
    </div>
  );
}

export function Panel({
  children,
  className,
  title
}: {
  children: ReactNode;
  className?: string;
  title?: string;
}) {
  return (
    <section className={cn("panel", className)}>
      {title ? <h2>{title}</h2> : null}
      {children}
    </section>
  );
}

export function StatusPill({ children, icon: Icon }: { children: ReactNode; icon?: IconComponent }) {
  return (
    <span className="statusPill">
      {Icon ? <Icon aria-hidden="true" size={15} strokeWidth={2.3} /> : null}
      <span>{children}</span>
    </span>
  );
}

export function StatGrid({
  items
}: {
  items: Array<{ label: string; value: ReactNode; icon?: IconComponent }>;
}) {
  return (
    <div className="statGrid">
      {items.map((item) => {
        const Icon = item.icon;
        return (
          <div className="statTile" key={item.label}>
            {Icon ? <Icon aria-hidden="true" size={18} strokeWidth={2.2} /> : null}
            <span>{item.label}</span>
            <strong>{item.value}</strong>
          </div>
        );
      })}
    </div>
  );
}
