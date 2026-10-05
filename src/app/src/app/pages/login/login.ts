import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth';
import { Lang } from '../../core/i18n';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly i18n = inject(Lang);

  protected readonly password = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  protected async submit() {
    this.error.set('');
    this.busy.set(true);
    try {
      await this.auth.login(this.password().trim());
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/recipes';
      void this.router.navigateByUrl(returnUrl);
    } catch {
      this.error.set(this.auth.passwordUnavailable() ? this.i18n.t('login.server') : this.i18n.t('login.wrong'));
    } finally {
      this.busy.set(false);
    }
  }
}
