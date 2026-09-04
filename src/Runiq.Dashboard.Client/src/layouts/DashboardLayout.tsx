import {
  Bot,
  Cable,
  Search,
  GitBranch,
  LogOut,
  PanelLeftOpen,
  Wrench,
  type LucideIcon,
} from 'lucide-react';
import { useState, type ReactNode } from 'react';

import { SidebarItem } from '../components/Sidebar/SidebarItem';
import { ThemeToggle } from '../components/ThemeToggle/ThemeToggle';
import {
  getDashboardLogoutPath,
  shouldShowDashboardLogout,
} from '../dashboardConfig';
import type { DashboardPage, DashboardRouteDefinition } from '../routes';

const navigationIcons: Record<DashboardPage, LucideIcon> = {
  agents: Bot,
  tools: Wrench,
  mcp: Cable,
  rag: Search,
  workflows: GitBranch,
};

const publicAsset = (fileName: string) =>
  `${import.meta.env.BASE_URL}${fileName}`;

export type DashboardBreadcrumb = {
  label: string;
  onClick?: () => void;
};

type DashboardLayoutProps = {
  title: string;
  subtitle?: string;
  breadcrumbs?: DashboardBreadcrumb[];
  activePage: DashboardPage;
  dashboardTitle: string;
  navigationRoutes: DashboardRouteDefinition[];
  children: ReactNode;
  onNavigate: (page: DashboardPage) => void;
  onLogoClick: () => void;
};

export function DashboardLayout({
  title,
  subtitle,
  breadcrumbs,
  activePage,
  navigationRoutes,
  children,
  onNavigate,
  onLogoClick,
}: DashboardLayoutProps) {
  const [isSidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [isMobileSidebarOpen, setMobileSidebarOpen] = useState(false);
  const showLogout = shouldShowDashboardLogout();

  const closeMobileSidebar = () => {
    setMobileSidebarOpen(false);
  };

  const navigateTo = (page: DashboardPage) => {
    onNavigate(page);
    closeMobileSidebar();
  };

  const handleLogoClick = () => {
    onLogoClick();
    closeMobileSidebar();
  };

  return (
    <main className="flex h-dvh w-full overflow-hidden bg-[var(--runiq-page-surface)] text-zinc-950 dark:text-zinc-100">
      {isMobileSidebarOpen && (
        <button
          type="button"
          aria-label="Close navigation backdrop"
          className="fixed inset-0 z-30 bg-black/60 backdrop-blur-sm lg:hidden"
          onClick={closeMobileSidebar}
        />
      )}

      <aside
        className={[
          'fixed inset-y-0 left-0 z-40 flex h-dvh flex-col border-r border-zinc-200 bg-[var(--runiq-sidebar-surface)] p-5 shadow-2xl transition-all duration-200 dark:border-zinc-800 lg:static lg:z-auto lg:shadow-none',
          isMobileSidebarOpen
            ? 'translate-x-0'
            : '-translate-x-full lg:translate-x-0',
          isSidebarCollapsed
            ? 'static z-auto w-20 translate-x-0 px-3 shadow-none'
            : 'w-[320px] max-w-[calc(100vw-48px)] lg:w-[270px]',
        ].join(' ')}
      >
        <button
          type="button"
          aria-label="Go to dashboard home"
          title="Dashboard home"
          onClick={handleLogoClick}
          className={[
            'flex min-h-10 items-center gap-3 rounded-lg text-left transition focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-500',
            isSidebarCollapsed ? 'justify-center' : '',
          ].join(' ')}
        >
          {isSidebarCollapsed ? (
            <img
              src={publicAsset('runiq-icon.png')}
              alt="RunIQ"
              className="size-11 object-contain"
            />
          ) : (
            <div className="flex min-w-0 items-center gap-3">
              <img
                src={publicAsset('runiq-logo-light.png')}
                alt="RunIQ"
                className="h-14 w-auto max-w-[210px] object-contain dark:hidden"
              />
              <img
                src={publicAsset('runiq-logo-dark.png')}
                alt="RunIQ"
                className="hidden h-14 w-auto max-w-[210px] object-contain dark:block"
              />
            </div>
          )}
        </button>

        <div className={isSidebarCollapsed ? 'mt-9 lg:mt-10' : 'mt-9'}>
          <div
            className={[
              'mb-3 px-1 text-[11px] font-medium uppercase tracking-[0.18em] text-zinc-500 dark:text-zinc-500',
              isSidebarCollapsed ? 'hidden' : '',
            ].join(' ')}
          >
            <span>Primitives</span>
          </div>

          <nav className="flex flex-col gap-1">
            {navigationRoutes.map((route) => (
              <SidebarItem
                key={route.page}
                label={route.navLabel}
                active={activePage === route.page}
                collapsed={isSidebarCollapsed}
                icon={navigationIcons[route.page]}
                onClick={() => navigateTo(route.page)}
              />
            ))}
          </nav>
        </div>

        <div className="mt-auto">
          <div className={isSidebarCollapsed ? 'flex justify-center' : 'flex justify-end'}>
            <button
              type="button"
              className="inline-flex size-10 shrink-0 items-center justify-center rounded-xl border border-zinc-200 bg-white text-zinc-700 shadow-sm transition hover:bg-zinc-100 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-100 dark:hover:border-zinc-700 dark:hover:bg-zinc-900"
              aria-label={
                isSidebarCollapsed ? 'Expand navigation' : 'Collapse navigation'
              }
              aria-expanded={!isSidebarCollapsed}
              onClick={() => {
                if (isSidebarCollapsed) {
                  setSidebarCollapsed(false);
                  setMobileSidebarOpen(
                    window.matchMedia('(max-width: 1023px)').matches,
                  );
                  return;
                }

                setMobileSidebarOpen(false);
                setSidebarCollapsed(true);
              }}
            >
              <SidebarCollapseIcon
                direction={isSidebarCollapsed ? 'right' : 'left'}
              />
            </button>
          </div>
        </div>
      </aside>

      <section className="flex min-w-0 flex-1 flex-col">
<header className="flex h-[88px] shrink-0 items-center justify-between gap-4 border-b border-zinc-200 bg-[var(--runiq-page-surface)] px-6 dark:border-zinc-800 lg:px-10">
  <div className="flex min-w-0 items-center gap-4">
    <button
      type="button"
      className="inline-flex size-10 items-center justify-center rounded-xl border border-zinc-200 bg-white text-zinc-700 transition hover:border-zinc-300 hover:bg-zinc-100 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-100 dark:hover:border-zinc-700 dark:hover:bg-zinc-900 lg:hidden"
      aria-label="Open navigation"
      aria-expanded={isMobileSidebarOpen}
      onClick={() => setMobileSidebarOpen(true)}
    >
      <PanelLeftOpen size={19} strokeWidth={2} aria-hidden="true" />
    </button>

    <div className="min-w-0">
      <h1 className="m-0 truncate text-[22px] font-bold leading-7 tracking-tight text-zinc-950 dark:text-zinc-100">
        {title}
      </h1>

      {breadcrumbs && breadcrumbs.length > 0 ? (
        <nav
          aria-label="Breadcrumb"
          className="mt-1 flex h-5 min-w-0 items-center gap-1 text-sm text-zinc-500 dark:text-zinc-500"
        >
          {breadcrumbs.map((breadcrumb, index) => {
            const isLast = index === breadcrumbs.length - 1;

            return (
              <span
                key={`${breadcrumb.label}-${index}`}
                className="flex min-w-0 items-center gap-1"
              >
                {breadcrumb.onClick && !isLast ? (
                  <button
                    type="button"
                    onClick={breadcrumb.onClick}
                    className="truncate font-medium text-zinc-500 transition hover:text-zinc-950 dark:text-zinc-500 dark:hover:text-zinc-100"
                  >
                    {breadcrumb.label}
                  </button>
                ) : (
                  <span
                    className={[
                      'truncate',
                      isLast ? 'text-zinc-700 dark:text-zinc-300' : '',
                    ].join(' ')}
                  >
                    {breadcrumb.label}
                  </span>
                )}

                {!isLast && (
                  <span className="shrink-0 text-zinc-400 dark:text-zinc-600">
                    /
                  </span>
                )}
              </span>
            );
          })}
        </nav>
      ) : subtitle ? (
        <p className="m-0 mt-1 truncate text-sm text-zinc-500 dark:text-zinc-500">
          {subtitle}
        </p>
      ) : (
        <div className="mt-1 h-5" aria-hidden="true" />
      )}
    </div>
  </div>

  <div className="flex shrink-0 items-center gap-2">
    {showLogout && (
      <button
        type="button"
        aria-label="Log out"
        title="Log out"
        onClick={() => {
          window.location.assign(getDashboardLogoutPath());
        }}
        className="inline-flex size-10 items-center justify-center rounded-xl border border-zinc-200 bg-white text-zinc-700 shadow-sm transition hover:bg-zinc-100 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-100 dark:hover:border-zinc-700 dark:hover:bg-zinc-900"
      >
        <LogOut size={18} strokeWidth={2} aria-hidden="true" />
      </button>
    )}
    <ThemeToggle />
  </div>
</header>

        <div className="min-h-0 flex-1 overflow-auto p-6 lg:p-10">
          {children}
        </div>
      </section>
    </main>
  );
}

type SidebarCollapseIconProps = {
  direction: 'left' | 'right';
};

function SidebarCollapseIcon({ direction }: SidebarCollapseIconProps) {
  const arrowPath =
    direction === 'left' ? 'M17 8L13 12L17 16' : 'M13 8L17 12L13 16';
  const arrowLine = direction === 'left' ? 'M13 12H22' : 'M8 12H17';

  return (
    <svg
      className="size-5"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth="1.8"
      aria-hidden="true"
    >
      <path d="M7 5V19" />
      <path d={arrowLine} />
      <path d={arrowPath} />
    </svg>
  );
}
