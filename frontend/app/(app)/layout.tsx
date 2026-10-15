"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { useStockFeed } from "@/lib/signalr";
import { Badge, Button, Spinner } from "@/components/ui";
import { PendingOperationRecovery } from "@/components/pending-operation-recovery";

const navigation = [
  { href: "/dashboard", label: "Dashboard", adminOnly: false },
  { href: "/parts", label: "Parts", adminOnly: false },
  { href: "/sales", label: "Counter sale", adminOnly: false },
  { href: "/returns", label: "Returns", adminOnly: false },
  { href: "/customers", label: "Customers", adminOnly: false },
  { href: "/movements", label: "Movements", adminOnly: false },
  { href: "/categories", label: "Categories", adminOnly: false },
  { href: "/brands", label: "Brands", adminOnly: false },
  { href: "/suppliers", label: "Suppliers", adminOnly: true },
  { href: "/receiving", label: "Receiving", adminOnly: true },
  { href: "/imports", label: "Imports", adminOnly: true },
  { href: "/cash", label: "Cash out", adminOnly: true },
  { href: "/pricing", label: "Pricing rules", adminOnly: true },
  { href: "/users", label: "Users", adminOnly: true },
];

const transactionReleaseEnabled = process.env.NEXT_PUBLIC_TRANSACTION_RELEASE_ENABLED === "true";
const transactionPages = ["/sales", "/returns", "/customers", "/cash", "/pricing"];

export default function AppLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const { user, status, signOut, isAdmin, canViewPrivate, showPrivate, hidePrivate } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const [menuOpen, setMenuOpen] = useState(false);

  const feed = useStockFeed({ enabled: status === "authenticated" });

  useEffect(() => {
    if (status === "anonymous") {
      router.replace("/login");
    } else if (status === "authenticated" && !transactionReleaseEnabled &&
      transactionPages.some((page) => pathname === page || pathname.startsWith(`${page}/`))) {
      router.replace("/dashboard");
    }
  }, [status, router, pathname]);

  if (status !== "authenticated" || !user) {
    return (
      <main className="flex min-h-screen items-center justify-center">
        <Spinner label="Checking your session…" />
      </main>
    );
  }

  if (!transactionReleaseEnabled &&
    transactionPages.some((page) => pathname === page || pathname.startsWith(`${page}/`))) {
    return <main className="flex min-h-screen items-center justify-center"><Spinner label="Returning to catalogue…" /></main>;
  }

  const links = navigation.filter((item) =>
    (!item.adminOnly || canViewPrivate) &&
    (transactionReleaseEnabled || !transactionPages.includes(item.href)));

  return (
    <div className="min-h-screen">
      {transactionReleaseEnabled && <PendingOperationRecovery actorId={user.id} />}
      <header className="sticky top-0 z-40 border-b border-border bg-surface/95 backdrop-blur">
        <div className="mx-auto flex max-w-7xl items-center gap-4 px-4 py-3">
          <Link href="/dashboard" className="text-base font-semibold">
            InventStock
          </Link>

          <nav className="hidden gap-1 md:flex">
            {links.map((item) => {
              const active = pathname.startsWith(item.href);
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  aria-current={active ? "page" : undefined}
                  className={`rounded-lg px-3 py-1.5 text-sm transition ${
                    active
                      ? "bg-surface-muted font-medium text-foreground"
                      : "text-muted hover:text-foreground"
                  }`}
                >
                  {item.label}
                </Link>
              );
            })}
          </nav>

          <div className="ml-auto flex items-center gap-3">
            {isAdmin && (
              <Button variant={canViewPrivate ? "danger" : "secondary"} onClick={canViewPrivate ? hidePrivate : showPrivate}>
                {canViewPrivate ? "Hide private costs" : "Reveal admin costs"}
              </Button>
            )}
            <span
              title={
                feed.status === "live"
                  ? "Receiving live stock updates"
                  : "Live updates unavailable"
              }
              className="hidden items-center gap-1.5 text-xs text-muted sm:flex"
            >
              <span
                aria-hidden
                className={`size-2 rounded-full ${
                  feed.status === "live"
                    ? "bg-success"
                    : feed.status === "connecting"
                      ? "bg-warning"
                      : "bg-border"
                }`}
              />
              {feed.status === "live" ? "Live" : feed.status}
            </span>

            <div className="hidden text-right sm:block">
              <p className="text-sm font-medium leading-tight">{user.fullName}</p>
              <p className="text-xs text-muted">{user.roles.join(", ")}</p>
            </div>

            <Button variant="secondary" onClick={() => void signOut()}>
              Sign out
            </Button>

            <Button
              variant="ghost"
              className="md:hidden"
              aria-expanded={menuOpen}
              aria-label="Toggle navigation"
              onClick={() => setMenuOpen((open) => !open)}
            >
              ☰
            </Button>
          </div>
        </div>

        {menuOpen && (
          <nav className="flex flex-col gap-1 border-t border-border px-4 py-2 md:hidden">
            {links.map((item) => (
              <Link
                key={item.href}
                href={item.href}
                onClick={() => setMenuOpen(false)}
                className="rounded-lg px-3 py-2 text-sm text-muted hover:bg-surface-muted hover:text-foreground"
              >
                {item.label}
              </Link>
            ))}
          </nav>
        )}
      </header>

      <main className="mx-auto max-w-7xl space-y-6 px-4 py-6">
        {feed.lastChange && (
          <div className="flex flex-wrap items-center gap-2 rounded-lg border border-border bg-surface px-4 py-2 text-sm">
            <Badge tone="accent">Live</Badge>
            <span className="text-muted">
              {feed.lastChange.partNumber} is now at{" "}
              <strong className="text-foreground">
                {feed.lastChange.stockConfidence === "NotCounted" ? "not counted" : feed.lastChange.quantityOnHand}
              </strong>{" "}
              {feed.lastChange.stockConfidence === "NotCounted" ? "" : feed.lastChange.stockConfidence === "NeedsRecount" ? "units · recount needed" : "units"} — {feed.lastChange.performedByUserName}
            </span>
            {feed.lastChange.isLowStock && <Badge tone="warning">Low stock</Badge>}
          </div>
        )}

        {children}
      </main>
    </div>
  );
}
