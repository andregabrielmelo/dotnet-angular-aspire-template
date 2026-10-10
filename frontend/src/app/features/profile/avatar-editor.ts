import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  input,
  signal,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { UsersService } from '../users/users.service';

/** Upload limits, mirroring the API's (AvatarLimits); the API enforces them regardless. */
export const AVATAR_MAX_BYTES = 2 * 1024 * 1024;
export const AVATAR_ACCEPT = 'image/jpeg,image/png,image/webp';

/**
 * Shows, replaces and removes the signed-in user's avatar. The API re-encodes every upload,
 * so what's shown is always what was stored, never the original file.
 */
@Component({
  selector: 'app-avatar-editor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './avatar-editor.html',
})
export class AvatarEditor implements OnInit {
  private readonly users = inject(UsersService);

  readonly userId = input.required<number>();

  readonly imageUrl = signal<string | null>(null);
  readonly busy = signal(false);
  readonly errorMessage = signal<string | null>(null);
  protected readonly accept = AVATAR_ACCEPT;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.setImage(null));
  }

  ngOnInit(): void {
    this.load();
  }

  upload(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    if (file.size > AVATAR_MAX_BYTES) {
      this.errorMessage.set('The image must be at most 2 MB.');
      return;
    }

    this.busy.set(true);
    this.errorMessage.set(null);
    this.users.uploadAvatar(this.userId(), file).subscribe({
      next: () => this.load(),
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.errorMessage.set(this.messageFor(error));
      },
    });
  }

  remove(): void {
    this.busy.set(true);
    this.errorMessage.set(null);
    this.users.deleteAvatar(this.userId()).subscribe({
      next: () => {
        this.setImage(null);
        this.busy.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.errorMessage.set(this.messageFor(error));
      },
    });
  }

  private load(): void {
    this.users.getAvatar(this.userId()).subscribe({
      next: (blob) => {
        this.setImage(URL.createObjectURL(blob));
        this.busy.set(false);
      },
      error: () => {
        this.setImage(null); // 404: no avatar yet
        this.busy.set(false);
      },
    });
  }

  private setImage(url: string | null): void {
    const previous = this.imageUrl();
    if (previous) {
      URL.revokeObjectURL(previous);
    }
    this.imageUrl.set(url);
  }

  private messageFor(error: HttpErrorResponse): string {
    switch (error.status) {
      case 400:
        return error.error?.errors?.file?.[0] ?? 'That image can’t be used.';
      case 413:
        return 'The image must be at most 2 MB.';
      case 429:
        return 'Too many uploads. Please wait a minute and try again.';
      case 503:
        return 'Images can’t be saved right now. Please try again later.';
      default:
        return 'Your avatar could not be saved. Please try again later.';
    }
  }
}
