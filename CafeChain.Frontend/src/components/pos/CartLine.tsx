import { usePreferences } from '../../contexts/PreferencesContext'

interface CartLineProps {
  index: number
  name: string
  optionSummary: string
  customerNote?: string
  quantity: number
  lineTotal: string
  locked: boolean
  onDecrease: () => void
  onIncrease: () => void
  onEdit: () => void
  onRemove: () => void
}

export default function CartLine({
  index,
  name,
  optionSummary,
  customerNote,
  quantity,
  lineTotal,
  locked,
  onDecrease,
  onIncrease,
  onEdit,
  onRemove,
}: CartLineProps) {
  const { t } = usePreferences()
  return (
    <article className="pos-cart-line rounded-lg border border-border-light bg-surface p-3">
      <div className="flex min-w-0 items-start justify-between gap-3">
        <div className="min-w-0 flex-1">
          <h3 className="line-clamp-2 text-sm font-bold text-text-primary">
            <span className="mr-1 text-text-muted">{index}.</span>{name}
          </h3>
          <p className="mt-1 line-clamp-2 text-xs font-semibold leading-4 text-text-secondary">{optionSummary}</p>
          {customerNote && (
            <p className="mt-1 line-clamp-2 text-xs leading-4 text-text-secondary">
              <span className="font-bold">{t('modifier.note')}:</span> {customerNote}
            </p>
          )}
        </div>
        <strong className="shrink-0 text-sm font-extrabold text-brand-orange tabular-nums">{lineTotal}</strong>
      </div>

      <div className="mt-3 flex items-center justify-between gap-2">
        <div className="flex items-center gap-1.5" aria-label={t('modifier.quantityValue', { quantity })}>
          <button
            type="button"
            onClick={onDecrease}
            disabled={locked}
            className="pos-touch-target flex items-center justify-center rounded-lg border border-border bg-white text-lg font-bold text-text-secondary hover:bg-brand-orange-light hover:text-brand-orange disabled:cursor-not-allowed disabled:opacity-40"
            aria-label={t('cart.decreaseQuantity', { name })}
          >
            −
          </button>
          <span className="w-8 text-center text-sm font-extrabold text-text-primary tabular-nums">{quantity}</span>
          <button
            type="button"
            onClick={onIncrease}
            disabled={locked}
            className="pos-touch-target flex items-center justify-center rounded-lg bg-brand-orange text-lg font-bold text-white hover:bg-brand-orange-hover disabled:cursor-not-allowed disabled:opacity-40"
            aria-label={t('cart.increaseQuantity', { name })}
          >
            +
          </button>
        </div>

        <details className="pos-cart-line-more relative">
          <summary className="pos-touch-target flex cursor-pointer list-none items-center justify-center rounded-lg border border-border bg-white px-3 text-xs font-bold text-text-primary hover:bg-surface-hover">
            {t('cart.actions')}
          </summary>
          <div className="absolute bottom-[calc(100%+6px)] right-0 z-20 grid min-w-36 gap-1 rounded-lg border border-border bg-white p-1.5 shadow-lg">
            <button
              type="button"
              onClick={onEdit}
              disabled={locked}
              className="min-h-11 rounded-md px-3 text-left text-xs font-bold text-text-primary hover:bg-brand-orange-light disabled:cursor-not-allowed disabled:opacity-40"
            >
              {t('cart.editItem')}
            </button>
            <button
              type="button"
              onClick={onRemove}
              disabled={locked}
              className="min-h-11 rounded-md px-3 text-left text-xs font-bold text-danger hover:bg-[var(--pos-danger-soft)] disabled:cursor-not-allowed disabled:opacity-40"
            >
              {t('cart.removeItem')}
            </button>
          </div>
        </details>
      </div>
    </article>
  )
}
